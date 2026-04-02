using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class BankingFunctions
    {
        private readonly ILogger<BankingFunctions> _logger;
        private readonly IBankAccountRepository? _bankAccountRepository;
        private readonly IBankTransactionRepository? _bankTransactionRepository;
        private readonly ICategorizationRuleRepository? _categorizationRuleRepository;
        private readonly DeletionGuardService? _guard;

        public BankingFunctions(
            ILogger<BankingFunctions> logger,
            IBankAccountRepository? bankAccountRepository = null,
            IBankTransactionRepository? bankTransactionRepository = null,
            ICategorizationRuleRepository? categorizationRuleRepository = null,
            DeletionGuardService? guard = null)
        {
            _logger = logger;
            _bankAccountRepository = bankAccountRepository;
            _bankTransactionRepository = bankTransactionRepository;
            _categorizationRuleRepository = categorizationRuleRepository;
            _guard = guard;
        }

        [Function("GetBankAccounts")]
        public async Task<HttpResponseData> GetBankAccounts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bank/accounts")] HttpRequestData req)
        {
            if (_bankAccountRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank account repository not available" });
                return response;
            }

            var accounts = await _bankAccountRepository.GetAllAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(accounts);
            return ok;
        }

        [Function("CreateBankAccount")]
        public async Task<HttpResponseData> CreateBankAccount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bank/accounts")] HttpRequestData req)
        {
            if (_bankAccountRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank account repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var account = JsonSerializer.Deserialize<BankAccount>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (account == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid bank account payload" });
                return bad;
            }

            account.CreatedDate = DateTime.UtcNow;
            account.ModifiedDate = DateTime.UtcNow;
            var created = await _bankAccountRepository.CreateAsync(account);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(created);
            return ok;
        }

        [Function("UpdateBankAccount")]
        public async Task<HttpResponseData> UpdateBankAccount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "bank/accounts/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_bankAccountRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank account repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var account = JsonSerializer.Deserialize<BankAccount>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (account == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid bank account payload" });
                return bad;
            }

            account.Id = id;
            account.ModifiedDate = DateTime.UtcNow;
            var updated = await _bankAccountRepository.UpdateAsync(account);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(updated);
            return ok;
        }

        [Function("DeleteBankAccount")]
        public async Task<HttpResponseData> DeleteBankAccount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "bank/accounts/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_bankAccountRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank account repository not available" });
                return response;
            }

            if (_guard != null)
            {
                var blocked = await _guard.GuardAsync(req, "bank account");
                if (blocked != null) return blocked;
            }

            await _bankAccountRepository.DeleteAsync(id);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("GetBankTransactions")]
        public async Task<HttpResponseData> GetBankTransactions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bank/transactions")] HttpRequestData req)
        {
            if (_bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank transaction repository not available" });
                return response;
            }

            var transactions = await _bankTransactionRepository.GetAllAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(transactions);
            return ok;
        }

        [Function("GetBankTransactionsByAccount")]
        public async Task<HttpResponseData> GetBankTransactionsByAccount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bank/accounts/{id:int}/transactions")] HttpRequestData req,
            int id)
        {
            if (_bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank transaction repository not available" });
                return response;
            }

            var transactions = await _bankTransactionRepository.GetByAccountIdAsync(id);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(transactions);
            return ok;
        }

        [Function("CreateBankTransaction")]
        public async Task<HttpResponseData> CreateBankTransaction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bank/transactions")] HttpRequestData req)
        {
            if (_bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank transaction repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var transaction = JsonSerializer.Deserialize<BankTransaction>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (transaction == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid bank transaction payload" });
                return bad;
            }

            transaction.CreatedDate = DateTime.UtcNow;
            transaction.ModifiedDate = DateTime.UtcNow;
            var created = await _bankTransactionRepository.CreateAsync(transaction);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(created);
            return ok;
        }

        [Function("ImportBankTransactions")]
        public async Task<HttpResponseData> ImportBankTransactions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bank/transactions/import")] HttpRequestData req)
        {
            if (_bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank transaction repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var transactions = JsonSerializer.Deserialize<List<BankTransaction>>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (transactions == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid transactions payload" });
                return bad;
            }

            foreach (var transaction in transactions)
            {
                transaction.CreatedDate = DateTime.UtcNow;
                transaction.ModifiedDate = DateTime.UtcNow;
            }

            // Auto-categorise uncategorised transactions using categorization rules
            if (_categorizationRuleRepository != null)
            {
                var catRules = (await _categorizationRuleRepository.GetActiveAsync()).ToList();
                if (catRules.Count > 0)
                {
                    foreach (var tx in transactions)
                    {
                        if (string.IsNullOrEmpty(tx.Category))
                        {
                            var matched = CategorizationFunctions.ApplyRulesToTransaction(tx, catRules);
                            if (matched != null)
                            {
                                tx.Category = matched;
                            }
                        }
                    }
                }
            }

            var created = await _bankTransactionRepository.CreateManyAsync(transactions);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(created);
            return ok;
        }

        [Function("UpdateBankTransaction")]
        public async Task<HttpResponseData> UpdateBankTransaction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "bank/transactions/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank transaction repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var transaction = JsonSerializer.Deserialize<BankTransaction>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (transaction == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid bank transaction payload" });
                return bad;
            }

            transaction.Id = id;
            transaction.ModifiedDate = DateTime.UtcNow;
            var updated = await _bankTransactionRepository.UpdateAsync(transaction);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(updated);
            return ok;
        }

        [Function("DeleteBankTransaction")]
        public async Task<HttpResponseData> DeleteBankTransaction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "bank/transactions/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_bankTransactionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Bank transaction repository not available" });
                return response;
            }

            if (_guard != null)
            {
                var blocked = await _guard.GuardAsync(req, "bank transaction");
                if (blocked != null) return blocked;
            }

            await _bankTransactionRepository.DeleteAsync(id);
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
