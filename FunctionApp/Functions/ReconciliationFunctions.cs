using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;

namespace FinanceHubFunctions.Functions
{
    public class ReconciliationFunctions
    {
        private readonly ILogger<ReconciliationFunctions> _logger;
        private readonly IBankTransactionRepository? _bankTransactionRepository;
        private readonly IReconciliationRuleRepository? _ruleRepository;
        private readonly IReconciliationMatchRepository? _matchRepository;
        private readonly DeletionGuardService? _guard;

        public ReconciliationFunctions(
            ILogger<ReconciliationFunctions> logger,
            IBankTransactionRepository? bankTransactionRepository = null,
            IReconciliationRuleRepository? ruleRepository = null,
            IReconciliationMatchRepository? matchRepository = null,
            DeletionGuardService? guard = null)
        {
            _logger = logger;
            _bankTransactionRepository = bankTransactionRepository;
            _ruleRepository = ruleRepository;
            _matchRepository = matchRepository;
            _guard = guard;
        }

        [Function("GetUnreconciledTransactions")]
        public async Task<HttpResponseData> GetUnreconciledTransactions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reconciliation/unreconciled")] HttpRequestData req)
        {
            if (_bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank transaction repository not available" });
                return response;
            }

            var transactions = await _bankTransactionRepository.GetUnreconciledAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(transactions);
            return ok;
        }

        [Function("CreateReconciliationMatch")]
        public async Task<HttpResponseData> CreateReconciliationMatch(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reconciliation/match")] HttpRequestData req)
        {
            if (_matchRepository == null || _bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Reconciliation services not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var match = JsonSerializer.Deserialize<ReconciliationMatch>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (match == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid reconciliation match payload" });
                return bad;
            }

            match.CreatedDate = DateTime.UtcNow;
            var created = await _matchRepository.CreateAsync(match);

            var transaction = await _bankTransactionRepository.GetByIdAsync(match.BankTransactionId);
            if (transaction != null)
            {
                transaction.IsReconciled = true;
                transaction.ReconciledOn = DateTime.UtcNow;
                await _bankTransactionRepository.UpdateAsync(transaction);
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(created);
            return ok;
        }

        [Function("GetReconciliationRules")]
        public async Task<HttpResponseData> GetReconciliationRules(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reconciliation/rules")] HttpRequestData req)
        {
            if (_ruleRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Rule repository not available" });
                return response;
            }

            var rules = await _ruleRepository.GetAllAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(rules);
            return ok;
        }

        [Function("CreateReconciliationRule")]
        public async Task<HttpResponseData> CreateReconciliationRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reconciliation/rules")] HttpRequestData req)
        {
            if (_ruleRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Rule repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var rule = JsonSerializer.Deserialize<ReconciliationRule>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rule == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid rule payload" });
                return bad;
            }

            rule.CreatedDate = DateTime.UtcNow;
            rule.ModifiedDate = DateTime.UtcNow;
            var created = await _ruleRepository.CreateAsync(rule);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(created);
            return ok;
        }

        [Function("UpdateReconciliationRule")]
        public async Task<HttpResponseData> UpdateReconciliationRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "reconciliation/rules/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_ruleRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Rule repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var rule = JsonSerializer.Deserialize<ReconciliationRule>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rule == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid rule payload" });
                return bad;
            }

            rule.Id = id;
            rule.ModifiedDate = DateTime.UtcNow;
            var updated = await _ruleRepository.UpdateAsync(rule);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(updated);
            return ok;
        }

        [Function("DeleteReconciliationRule")]
        public async Task<HttpResponseData> DeleteReconciliationRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "reconciliation/rules/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_ruleRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Rule repository not available" });
                return response;
            }

            if (_guard != null)
            {
                var blocked = await _guard.GuardAsync(req, "reconciliation rule");
                if (blocked != null) return blocked;
            }

            await _ruleRepository.DeleteAsync(id);
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
