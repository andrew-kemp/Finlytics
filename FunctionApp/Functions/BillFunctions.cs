using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class BillFunctions
    {
        private readonly IBillRepository _billRepo;
        private readonly ISupplierRepository _supplierRepo;
        private readonly ILogger<BillFunctions> _logger;
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public BillFunctions(
            IBillRepository billRepo,
            ISupplierRepository supplierRepo,
            ILogger<BillFunctions> logger)
        {
            _billRepo = billRepo;
            _supplierRepo = supplierRepo;
            _logger = logger;
        }

        // ──────── GET /api/bills ────────
        [Function("GetBills")]
        public async Task<HttpResponseData> GetBills(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bills")] HttpRequestData req)
        {
            var bills = await _billRepo.GetAllAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(bills);
            return response;
        }

        // ──────── GET /api/bills/{id} ────────
        [Function("GetBill")]
        public async Task<HttpResponseData> GetBill(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bills/{id:int}")] HttpRequestData req,
            int id)
        {
            var bill = await _billRepo.GetByIdAsync(id);
            if (bill == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(bill);
            return response;
        }

        // ──────── POST /api/bills ────────
        [Function("CreateBill")]
        public async Task<HttpResponseData> CreateBill(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bills")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var bill = JsonSerializer.Deserialize<Bill>(body, _jsonOpts);
                if (bill == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "Invalid bill data" });
                    return bad;
                }

                // Auto-generate bill number
                if (string.IsNullOrEmpty(bill.BillNumber))
                    bill.BillNumber = await _billRepo.GenerateNextBillNumberAsync();

                // Default dates
                if (bill.DateReceived == default)
                    bill.DateReceived = DateTime.UtcNow;

                // Auto-set overdue status
                UpdateBillStatus(bill);

                // Auto-create supplier if name provided but no ID
                if (string.IsNullOrEmpty(bill.SupplierId) && !string.IsNullOrEmpty(bill.SupplierName))
                {
                    var suppliers = await _supplierRepo.GetAllAsync();
                    var match = suppliers.FirstOrDefault(s =>
                        s.Name.Equals(bill.SupplierName, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        bill.SupplierId = match.Id;
                    }
                    else
                    {
                        var code = bill.SupplierName.Length >= 3
                            ? bill.SupplierName[..3].ToUpper().Replace(" ", "X")
                            : bill.SupplierName.ToUpper().PadRight(3, 'X');
                        var newSupplier = new Supplier
                        {
                            Id = Guid.NewGuid().ToString(),
                            Code = code,
                            SupplierCode = code,
                            Name = bill.SupplierName,
                            IsActive = true,
                            PayeeType = "Supplier"
                        };
                        await _supplierRepo.CreateAsync(newSupplier);
                        bill.SupplierId = newSupplier.Id;
                        _logger.LogInformation("Auto-created supplier '{Name}' with code '{Code}'", bill.SupplierName, code);
                    }
                }

                var created = await _billRepo.CreateAsync(bill);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(created);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bill");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ──────── PUT /api/bills/{id} ────────
        [Function("UpdateBill")]
        public async Task<HttpResponseData> UpdateBill(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "bills/{id:int}")] HttpRequestData req,
            int id)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var bill = JsonSerializer.Deserialize<Bill>(body, _jsonOpts);
                if (bill == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "Invalid bill data" });
                    return bad;
                }

                bill.Id = id;
                UpdateBillStatus(bill);

                var updated = await _billRepo.UpdateAsync(bill);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(updated);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bill {Id}", id);
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ──────── DELETE /api/bills/{id} ────────
        [Function("DeleteBill")]
        public async Task<HttpResponseData> DeleteBill(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "bills/{id:int}")] HttpRequestData req,
            int id)
        {
            await _billRepo.DeleteAsync(id);
            return req.CreateResponse(HttpStatusCode.NoContent);
        }

        // ──────── POST /api/bills/{id}/approve ────────
        [Function("ApproveBill")]
        public async Task<HttpResponseData> ApproveBill(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bills/{id:int}/approve")] HttpRequestData req,
            int id)
        {
            var bill = await _billRepo.GetByIdAsync(id);
            if (bill == null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            bill.Status = "Approved";
            bill.ApprovedAt = DateTime.UtcNow;

            // Read optional approver name from body
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrEmpty(body))
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body, _jsonOpts);
                    if (data != null && data.TryGetValue("approvedBy", out var approver))
                        bill.ApprovedBy = approver;
                }
            }
            catch { /* optional */ }

            await _billRepo.UpdateAsync(bill);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(bill);
            return response;
        }

        // ──────── POST /api/bills/{id}/pay ────────
        [Function("PayBill")]
        public async Task<HttpResponseData> PayBill(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bills/{id:int}/pay")] HttpRequestData req,
            int id)
        {
            var bill = await _billRepo.GetByIdAsync(id);
            if (bill == null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            // Read payment details from body
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrEmpty(body))
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body, _jsonOpts);
                    if (data != null)
                    {
                        if (data.TryGetValue("amountPaid", out var amt))
                            bill.AmountPaid = amt.GetDecimal();
                        if (data.TryGetValue("paymentMethod", out var pm))
                            bill.PaymentMethod = pm.GetString();
                        if (data.TryGetValue("paymentReference", out var pr))
                            bill.PaymentReference = pr.GetString();
                        if (data.TryGetValue("datePaid", out var dp))
                            bill.DatePaid = dp.GetDateTime();
                    }
                }
            }
            catch { /* use defaults */ }

            if (bill.AmountPaid == 0)
                bill.AmountPaid = bill.AmountGross;
            if (!bill.DatePaid.HasValue)
                bill.DatePaid = DateTime.UtcNow;

            bill.Status = bill.AmountPaid >= bill.AmountGross ? "Paid" : bill.Status;

            await _billRepo.UpdateAsync(bill);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(bill);
            return response;
        }

        // ──────── GET /api/bills/next-number ────────
        [Function("GetNextBillNumber")]
        public async Task<HttpResponseData> GetNextBillNumber(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bills/next-number")] HttpRequestData req)
        {
            var number = await _billRepo.GenerateNextBillNumberAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { billNumber = number });
            return response;
        }

        // ──────── GET /api/bills/overdue ────────
        [Function("GetOverdueBills")]
        public async Task<HttpResponseData> GetOverdueBills(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bills/overdue")] HttpRequestData req)
        {
            var bills = await _billRepo.GetOverdueAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(bills);
            return response;
        }

        // ──────── GET /api/bills/summary ────────
        [Function("GetBillsSummary")]
        public async Task<HttpResponseData> GetBillsSummary(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bills/summary")] HttpRequestData req)
        {
            var all = (await _billRepo.GetAllAsync()).ToList();
            var today = DateTime.UtcNow.Date;

            var unpaidBills = all.Where(b => b.Status != "Paid" && b.Status != "Cancelled" && b.Status != "Draft").ToList();
            var overdue = unpaidBills.Where(b => b.DueDate.HasValue && b.DueDate.Value.Date < today).ToList();
            var dueThisWeek = unpaidBills.Where(b => b.DueDate.HasValue && b.DueDate.Value.Date >= today && b.DueDate.Value.Date <= today.AddDays(7)).ToList();
            var dueThisMonth = unpaidBills.Where(b => b.DueDate.HasValue && b.DueDate.Value.Date >= today && b.DueDate.Value.Date <= today.AddDays(30)).ToList();

            // Aging buckets
            var overdue30 = overdue.Where(b => (today - b.DueDate!.Value.Date).Days <= 30).ToList();
            var overdue60 = overdue.Where(b => { var d = (today - b.DueDate!.Value.Date).Days; return d > 30 && d <= 60; }).ToList();
            var overdue90 = overdue.Where(b => (today - b.DueDate!.Value.Date).Days > 60).ToList();

            var paidThisMonth = all.Where(b => b.Status == "Paid" && b.DatePaid.HasValue &&
                b.DatePaid.Value.Year == today.Year && b.DatePaid.Value.Month == today.Month).ToList();

            var summary = new
            {
                totalBills = all.Count,
                totalUnpaid = unpaidBills.Count,
                totalUnpaidAmount = unpaidBills.Sum(b => b.AmountGross - b.AmountPaid),
                overdueCount = overdue.Count,
                overdueAmount = overdue.Sum(b => b.AmountGross - b.AmountPaid),
                dueThisWeekCount = dueThisWeek.Count,
                dueThisWeekAmount = dueThisWeek.Sum(b => b.AmountGross - b.AmountPaid),
                dueThisMonthCount = dueThisMonth.Count,
                dueThisMonthAmount = dueThisMonth.Sum(b => b.AmountGross - b.AmountPaid),
                overdue0to30Count = overdue30.Count,
                overdue0to30Amount = overdue30.Sum(b => b.AmountGross - b.AmountPaid),
                overdue31to60Count = overdue60.Count,
                overdue31to60Amount = overdue60.Sum(b => b.AmountGross - b.AmountPaid),
                overdue61PlusCount = overdue90.Count,
                overdue61PlusAmount = overdue90.Sum(b => b.AmountGross - b.AmountPaid),
                paidThisMonthCount = paidThisMonth.Count,
                paidThisMonthAmount = paidThisMonth.Sum(b => b.AmountGross),
                awaitingApproval = all.Count(b => b.Status == "Awaiting Approval")
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(summary);
            return response;
        }

        private static void UpdateBillStatus(Bill bill)
        {
            if (bill.Status == "Paid" || bill.Status == "Cancelled") return;

            if (bill.DatePaid.HasValue && bill.AmountPaid >= bill.AmountGross)
            {
                bill.Status = "Paid";
            }
            else if (bill.DueDate.HasValue && bill.DueDate.Value.Date < DateTime.UtcNow.Date
                     && bill.Status != "Draft")
            {
                bill.Status = "Overdue";
            }
        }
    }
}
