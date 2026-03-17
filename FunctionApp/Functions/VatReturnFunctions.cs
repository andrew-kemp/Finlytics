using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Services;

namespace FinanceHubFunctions.Functions
{
    public class VatReturnFunctions
    {
        private readonly ILogger<VatReturnFunctions> _logger;
        private readonly IVatReturnRepository _vatReturnRepository;
        private readonly DeletionGuardService _guard;

        public VatReturnFunctions(
            ILogger<VatReturnFunctions> logger,
            IVatReturnRepository vatReturnRepository,
            DeletionGuardService guard)
        {
            _logger = logger;
            _vatReturnRepository = vatReturnRepository;
            _guard = guard;
        }

        [Function("GetVatReturns")]
        public async Task<HttpResponseData> GetVatReturns(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "vat-returns")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all VAT returns");
            try
            {
                var returns = await _vatReturnRepository.GetAllAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(returns);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting VAT returns");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }

        [Function("CreateVatReturn")]
        public async Task<HttpResponseData> CreateVatReturn(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "vat-returns")] HttpRequestData req)
        {
            _logger.LogInformation("Creating VAT return");
            try
            {
                var vatReturn = await req.ReadFromJsonAsync<VatReturn>();
                if (vatReturn == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid VAT return data");
                    return bad;
                }

                vatReturn.Status = "Filed";
                vatReturn.CreatedDate = DateTime.UtcNow;
                vatReturn.ModifiedDate = DateTime.UtcNow;

                var created = await _vatReturnRepository.CreateAsync(vatReturn);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(created);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating VAT return");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }

        [Function("UpdateVatReturn")]
        public async Task<HttpResponseData> UpdateVatReturn(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "vat-returns/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation("Updating VAT return {Id}", id);
            try
            {
                var existing = await _vatReturnRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                var updated = await req.ReadFromJsonAsync<VatReturn>();
                if (updated == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid VAT return data");
                    return bad;
                }

                existing.QuarterLabel = updated.QuarterLabel;
                existing.MonthsLabel = updated.MonthsLabel;
                existing.QuarterStartDate = updated.QuarterStartDate;
                existing.QuarterEndDate = updated.QuarterEndDate;
                existing.VatIn = updated.VatIn;
                existing.VatOut = updated.VatOut;
                existing.VatOwed = updated.VatOwed;
                existing.FiledDate = updated.FiledDate;
                existing.Reference = updated.Reference;
                existing.Notes = updated.Notes;
                existing.ModifiedDate = DateTime.UtcNow;

                var result = await _vatReturnRepository.UpdateAsync(existing);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating VAT return {Id}", id);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }

        [Function("DeleteVatReturn")]
        public async Task<HttpResponseData> DeleteVatReturn(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "vat-returns/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation("Deleting VAT return {Id}", id);
            try
            {
                var blocked = await _guard.GuardAsync(req, "VAT return");
                if (blocked != null) return blocked;

                var existing = await _vatReturnRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                await _vatReturnRepository.DeleteAsync(id);
                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting VAT return {Id}", id);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }
    }
}
