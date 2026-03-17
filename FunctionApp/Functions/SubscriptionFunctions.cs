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
    public class SubscriptionFunctions
    {
        private readonly ILogger<SubscriptionFunctions> _logger;
        private readonly ISubscriptionRepository? _subscriptionRepository;
        private readonly DeletionGuardService? _guard;

        public SubscriptionFunctions(
            ILogger<SubscriptionFunctions> logger,
            ISubscriptionRepository? subscriptionRepository = null,
            DeletionGuardService? guard = null)
        {
            _logger = logger;
            _subscriptionRepository = subscriptionRepository;
            _guard = guard;
        }

        [Function("GetSubscriptions")]
        public async Task<HttpResponseData> GetSubscriptions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "subscriptions")] HttpRequestData req)
        {
            if (_subscriptionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Subscription repository not available" });
                return response;
            }

            var subscriptions = await _subscriptionRepository.GetAllAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(subscriptions);
            return ok;
        }

        [Function("GetSubscriptionById")]
        public async Task<HttpResponseData> GetSubscriptionById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "subscriptions/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_subscriptionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Subscription repository not available" });
                return response;
            }

            var subscription = await _subscriptionRepository.GetByIdAsync(id);
            if (subscription == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(subscription);
            return ok;
        }

        [Function("GetNextSubscriptionId")]
        public async Task<HttpResponseData> GetNextSubscriptionId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "subscriptions/next-id")] HttpRequestData req)
        {
            if (_subscriptionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Subscription repository not available" });
                return response;
            }

            var nextId = await _subscriptionRepository.GenerateNextSubscriptionIdAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(new { nextId });
            return ok;
        }

        [Function("GetExpiringSubscriptions")]
        public async Task<HttpResponseData> GetExpiringSubscriptions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "subscriptions/expiring/{days:int}")] HttpRequestData req,
            int days)
        {
            if (_subscriptionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Subscription repository not available" });
                return response;
            }

            var expiring = await _subscriptionRepository.GetExpiringWithinDaysAsync(days);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(expiring);
            return ok;
        }

        [Function("CreateSubscription")]
        public async Task<HttpResponseData> CreateSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "subscriptions")] HttpRequestData req)
        {
            if (_subscriptionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Subscription repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var subscription = JsonSerializer.Deserialize<Subscription>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (subscription == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid subscription payload" });
                return bad;
            }

            if (string.IsNullOrWhiteSpace(subscription.SubscriptionId))
            {
                subscription.SubscriptionId = await _subscriptionRepository.GenerateNextSubscriptionIdAsync();
            }

            subscription.CreatedDate = DateTime.UtcNow;
            subscription.ModifiedDate = DateTime.UtcNow;

            var created = await _subscriptionRepository.CreateAsync(subscription);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(created);
            return ok;
        }

        [Function("UpdateSubscription")]
        public async Task<HttpResponseData> UpdateSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "subscriptions/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_subscriptionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Subscription repository not available" });
                return response;
            }

            var existing = await _subscriptionRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var subscription = JsonSerializer.Deserialize<Subscription>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (subscription == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid subscription payload" });
                return bad;
            }

            subscription.Id = id;
            subscription.SubscriptionId = existing.SubscriptionId;
            subscription.CreatedDate = existing.CreatedDate;
            subscription.ModifiedDate = DateTime.UtcNow;

            var updated = await _subscriptionRepository.UpdateAsync(subscription);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(updated);
            return ok;
        }

        [Function("DeleteSubscription")]
        public async Task<HttpResponseData> DeleteSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "subscriptions/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_subscriptionRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Subscription repository not available" });
                return response;
            }

            if (_guard != null)
            {
                var blocked = await _guard.GuardAsync(req, "subscription");
                if (blocked != null) return blocked;
            }

            await _subscriptionRepository.DeleteAsync(id);
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
