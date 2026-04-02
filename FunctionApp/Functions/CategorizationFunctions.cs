using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class CategorizationFunctions
    {
        private readonly ILogger _logger;
        private readonly ICategorizationRuleRepository _ruleRepository;
        private readonly IBankTransactionRepository _bankTransactionRepository;

        public CategorizationFunctions(
            ILoggerFactory loggerFactory,
            ICategorizationRuleRepository ruleRepository,
            IBankTransactionRepository bankTransactionRepository)
        {
            _logger = loggerFactory.CreateLogger<CategorizationFunctions>();
            _ruleRepository = ruleRepository;
            _bankTransactionRepository = bankTransactionRepository;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  GET /api/categorization-rules — List all rules
        // ═══════════════════════════════════════════════════════════════════════

        [Function("GetCategorizationRules")]
        public async Task<HttpResponseData> GetRules(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "categorization-rules")] HttpRequestData req)
        {
            try
            {
                var rules = await _ruleRepository.GetAllAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(rules);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categorization rules");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  GET /api/categorization-rules/{id}
        // ═══════════════════════════════════════════════════════════════════════

        [Function("GetCategorizationRule")]
        public async Task<HttpResponseData> GetRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "categorization-rules/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                var rule = await _ruleRepository.GetByIdAsync(id);
                if (rule == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(rule);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categorization rule");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  POST /api/categorization-rules — Create rule
        // ═══════════════════════════════════════════════════════════════════════

        [Function("CreateCategorizationRule")]
        public async Task<HttpResponseData> CreateRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "categorization-rules")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var rule = JsonSerializer.Deserialize<CategorizationRule>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (rule == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid rule data");
                    return badResponse;
                }

                var created = await _ruleRepository.CreateAsync(rule);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(created);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating categorization rule");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PUT /api/categorization-rules/{id} — Update rule
        // ═══════════════════════════════════════════════════════════════════════

        [Function("UpdateCategorizationRule")]
        public async Task<HttpResponseData> UpdateRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "categorization-rules/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                var existing = await _ruleRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var rule = JsonSerializer.Deserialize<CategorizationRule>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (rule == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid rule data");
                    return badResponse;
                }

                rule.Id = id;
                var updated = await _ruleRepository.UpdateAsync(rule);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(updated);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating categorization rule");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  DELETE /api/categorization-rules/{id}
        // ═══════════════════════════════════════════════════════════════════════

        [Function("DeleteCategorizationRule")]
        public async Task<HttpResponseData> DeleteRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "categorization-rules/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                var existing = await _ruleRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                await _ruleRepository.DeleteAsync(id);
                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting categorization rule");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  POST /api/categorization-rules/apply — Apply rules to uncategorised transactions
        // ═══════════════════════════════════════════════════════════════════════

        [Function("ApplyCategorizationRules")]
        public async Task<HttpResponseData> ApplyRules(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "categorization-rules/apply")] HttpRequestData req)
        {
            try
            {
                var rules = (await _ruleRepository.GetActiveAsync()).ToList();
                if (!rules.Any())
                {
                    var noRulesResponse = req.CreateResponse(HttpStatusCode.OK);
                    await noRulesResponse.WriteAsJsonAsync(new { categorised = 0, message = "No active rules configured" });
                    return noRulesResponse;
                }

                // Get all uncategorised transactions
                var allTransactions = await _bankTransactionRepository.GetAllAsync();
                var uncategorised = allTransactions
                    .Where(t => string.IsNullOrEmpty(t.Category))
                    .ToList();

                int categorised = 0;
                foreach (var tx in uncategorised)
                {
                    var matchedCategory = ApplyRulesToTransaction(tx, rules);
                    if (matchedCategory != null)
                    {
                        tx.Category = matchedCategory;
                        tx.ModifiedDate = DateTime.UtcNow;
                        await _bankTransactionRepository.UpdateAsync(tx);
                        categorised++;
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    categorised,
                    total = uncategorised.Count,
                    message = $"Categorised {categorised} of {uncategorised.Count} uncategorised transactions"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying categorization rules");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Static helper: apply rules to a single transaction (used by TrueLayer sync too)
        // ═══════════════════════════════════════════════════════════════════════

        public static string? ApplyRulesToTransaction(BankTransaction tx, IEnumerable<CategorizationRule> rules)
        {
            foreach (var rule in rules.OrderBy(r => r.Priority))
            {
                if (!rule.IsActive) continue;

                // Check direction filter
                if (!string.IsNullOrEmpty(rule.Direction) && !string.Equals(rule.Direction, tx.Direction, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check amount range
                if (rule.AmountMin.HasValue && tx.Amount < rule.AmountMin.Value) continue;
                if (rule.AmountMax.HasValue && tx.Amount > rule.AmountMax.Value) continue;

                // Get the field to match against
                var fieldValue = rule.MatchField?.ToLowerInvariant() switch
                {
                    "merchantname" => tx.TrueLayerMerchantName ?? tx.MonzoMerchantName,
                    "reference" => tx.Reference,
                    _ => tx.Description // default to Description
                };

                if (string.IsNullOrEmpty(fieldValue) || string.IsNullOrEmpty(rule.MatchPattern))
                    continue;

                // Try regex match first, fall back to case-insensitive contains
                try
                {
                    if (Regex.IsMatch(fieldValue, rule.MatchPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                    {
                        return rule.TargetCategory;
                    }
                }
                catch (RegexParseException)
                {
                    // Not a valid regex — treat as substring match
                    if (fieldValue.Contains(rule.MatchPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return rule.TargetCategory;
                    }
                }
            }

            return null;
        }
    }
}
