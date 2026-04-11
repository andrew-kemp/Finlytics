using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceHubFunctions.Functions
{
    public class MileageFunctions
    {
        private readonly ILogger<MileageFunctions> _logger;
        private readonly IMileageTripRepository _tripRepository;
        private readonly IMileageClaimRepository _claimRepository;
        private readonly IDlaRepository _dlaRepository;
        private readonly ICompanyLedgerRepository _ledgerRepository;

        // Fallback defaults — overridden by CompanySettings.AmapRate45p/25p/ThresholdMiles
        private const decimal DefaultRate45p       = 0.45m;
        private const decimal DefaultRate25p       = 0.25m;
        private const decimal DefaultThresholdMiles = 10000m;

        private readonly ICompanySettingsRepository _settingsRepository;
        private readonly DeletionGuardService _guard;

        public MileageFunctions(
            ILogger<MileageFunctions> logger,
            IMileageTripRepository tripRepository,
            IMileageClaimRepository claimRepository,
            IDlaRepository dlaRepository,
            ICompanyLedgerRepository ledgerRepository,
            ICompanySettingsRepository settingsRepository,
            DeletionGuardService guard)
        {
            _logger = logger;
            _tripRepository = tripRepository;
            _claimRepository = claimRepository;
            _dlaRepository = dlaRepository;
            _ledgerRepository = ledgerRepository;
            _settingsRepository = settingsRepository;
            _guard = guard;
        }

        private async Task<(decimal Rate45p, decimal Rate25p, decimal Threshold)> GetAmapSettingsAsync()
        {
            var settings = await _settingsRepository.GetDefaultAsync();
            return (
                settings?.AmapRate45p     ?? DefaultRate45p,
                settings?.AmapRate25p     ?? DefaultRate25p,
                settings?.AmapThresholdMiles ?? DefaultThresholdMiles
            );
        }

        // ─────────────────────────────────────────────────────────────────
        // Helper: compute UK tax year  (6 Apr YYYY → 5 Apr YYYY+1 = "YYYY/YY+1")
        // ─────────────────────────────────────────────────────────────────
        private static string GetTaxYear(DateTime date)
        {
            int startYear = (date.Month > 4 || (date.Month == 4 && date.Day >= 6))
                ? date.Year
                : date.Year - 1;
            return $"{startYear}/{(startYear + 1) % 100:D2}";
        }

        // ─────────────────────────────────────────────────────────────────
        // Helper: split miles at 10,000 threshold, return (at45, at25)
        // ─────────────────────────────────────────────────────────────────
        private static (decimal at45, decimal at25) SplitMiles(decimal totalMiles, decimal priorMiles, decimal threshold)
        {
            decimal remaining45 = Math.Max(0, threshold - priorMiles);
            decimal at45 = Math.Min(totalMiles, remaining45);
            decimal at25 = Math.Max(0, totalMiles - remaining45);
            return (at45, at25);
        }

        // ─────────────────────────────────────────────────────────────────
        // GET /api/mileage/trips
        // ─────────────────────────────────────────────────────────────────
        [Function("GetMileageTrips")]
        public async Task<HttpResponseData> GetMileageTrips(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mileage/trips")] HttpRequestData req)
        {
            _logger.LogInformation("GetMileageTrips called");
            try
            {
                var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var taxYear  = qs["taxYear"];
                var director = qs["director"];

                IEnumerable<MileageTrip> trips;
                if (!string.IsNullOrEmpty(director) && !string.IsNullOrEmpty(taxYear))
                    trips = await _tripRepository.GetByDirectorAndTaxYearAsync(director, taxYear);
                else if (!string.IsNullOrEmpty(taxYear))
                    trips = await _tripRepository.GetByTaxYearAsync(taxYear);
                else
                    trips = await _tripRepository.GetAllAsync();

                // Optional status filter
                var status = qs["status"];
                if (!string.IsNullOrEmpty(status))
                    trips = trips.Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase));

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(trips);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mileage trips");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/mileage/trips
        // ─────────────────────────────────────────────────────────────────
        [Function("CreateMileageTrip")]
        public async Task<HttpResponseData> CreateMileageTrip(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mileage/trips")] HttpRequestData req)
        {
            _logger.LogInformation("CreateMileageTrip called");
            try
            {
                var body = await req.ReadAsStringAsync();
                var trip = JsonSerializer.Deserialize<MileageTrip>(body!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (trip == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid request body");
                    return bad;
                }

                // Ensure tax year is set
                if (string.IsNullOrWhiteSpace(trip.TaxYear))
                    trip.TaxYear = GetTaxYear(trip.TripDate);

                // Set default status
                if (string.IsNullOrWhiteSpace(trip.Status))
                    trip.Status = "Draft";

                // Calculate total miles (double if return journey)
                decimal totalMiles = trip.IsReturn ? trip.Miles * 2 : trip.Miles;
                trip.Miles = totalMiles; // store the actual total

                // Get cumulative miles already logged this tax year
                decimal priorMiles = await _tripRepository.GetCumulativeMilesByTaxYearAsync(trip.Director, trip.TaxYear);

                var (rate45p, rate25p, threshold) = await GetAmapSettingsAsync();
                var (at45, at25) = SplitMiles(totalMiles, priorMiles, threshold);
                trip.MilesAt45p  = at45;
                trip.MilesAt25p  = at25;
                trip.AmountAt45p = Math.Round(at45 * rate45p, 2);
                trip.AmountAt25p = Math.Round(at25 * rate25p, 2);
                trip.TotalAmount = trip.AmountAt45p + trip.AmountAt25p;

                // Generate trip ID
                trip.TripId = await _tripRepository.GenerateNextTripIdAsync();
                trip.CreatedAt = DateTime.UtcNow;

                var created = await _tripRepository.CreateAsync(trip);
                var resp = req.CreateResponse(HttpStatusCode.Created);
                await resp.WriteAsJsonAsync(created);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating mileage trip");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT /api/mileage/trips/{id}
        // ─────────────────────────────────────────────────────────────────
        [Function("UpdateMileageTrip")]
        public async Task<HttpResponseData> UpdateMileageTrip(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "mileage/trips/{id:int}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation("UpdateMileageTrip called for id {Id}", id);
            try
            {
                var existing = await _tripRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Trip not found");
                    return notFound;
                }
                if (existing.Status != "Draft")
                {
                    var locked = req.CreateResponse(HttpStatusCode.Conflict);
                    await locked.WriteStringAsync("Only Draft trips can be edited");
                    return locked;
                }

                var body = await req.ReadAsStringAsync();
                var update = JsonSerializer.Deserialize<MileageTrip>(body!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (update == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid request body");
                    return bad;
                }

                // Preserve locked fields
                update.Id        = existing.Id;
                update.TripId    = existing.TripId;
                update.Status    = existing.Status;
                update.ClaimId   = existing.ClaimId;
                update.CreatedAt = existing.CreatedAt;

                if (string.IsNullOrWhiteSpace(update.TaxYear))
                    update.TaxYear = GetTaxYear(update.TripDate);

                // Recalculate miles/amounts (exclude this trip from prior cumulative)
                decimal totalMiles = update.IsReturn ? update.Miles * 2 : update.Miles;
                update.Miles = totalMiles;

                // Cumulative miles excluding THIS trip
                decimal allMiles = await _tripRepository.GetCumulativeMilesByTaxYearAsync(update.Director, update.TaxYear);
                decimal priorMiles = allMiles - existing.Miles; // subtract old value

                var (rate45p, rate25p, threshold) = await GetAmapSettingsAsync();
                var (at45, at25) = SplitMiles(totalMiles, Math.Max(0, priorMiles), threshold);
                update.MilesAt45p  = at45;
                update.MilesAt25p  = at25;
                update.AmountAt45p = Math.Round(at45 * rate45p, 2);
                update.AmountAt25p = Math.Round(at25 * rate25p, 2);
                update.TotalAmount = update.AmountAt45p + update.AmountAt25p;

                var updated = await _tripRepository.UpdateAsync(update);
                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(updated);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating mileage trip");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // DELETE /api/mileage/trips/{id}
        // ─────────────────────────────────────────────────────────────────
        [Function("DeleteMileageTrip")]
        public async Task<HttpResponseData> DeleteMileageTrip(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "mileage/trips/{id:int}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation("DeleteMileageTrip called for id {Id}", id);
            try
            {
                var blocked = await _guard.GuardAsync(req, "mileage trip");
                if (blocked != null) return blocked;

                var existing = await _tripRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Trip not found");
                    return notFound;
                }
                if (existing.Status != "Draft")
                {
                    var locked = req.CreateResponse(HttpStatusCode.Conflict);
                    await locked.WriteStringAsync("Only Draft trips can be deleted");
                    return locked;
                }

                await _tripRepository.DeleteAsync(id);
                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting mileage trip");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // GET /api/mileage/summary  ?taxYear=2025/26&director=
        // ─────────────────────────────────────────────────────────────────
        [Function("GetMileageSummary")]
        public async Task<HttpResponseData> GetMileageSummary(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mileage/summary")] HttpRequestData req)
        {
            _logger.LogInformation("GetMileageSummary called");
            try
            {
                var qs       = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var taxYear  = qs["taxYear"] ?? GetTaxYear(DateTime.UtcNow);
                var director = qs["director"] ?? string.Empty;

                IEnumerable<MileageTrip> trips;
                if (!string.IsNullOrEmpty(director))
                    trips = await _tripRepository.GetByDirectorAndTaxYearAsync(director, taxYear);
                else
                    trips = await _tripRepository.GetByTaxYearAsync(taxYear);

                var tripList = trips.ToList();
                decimal totalMiles  = tripList.Sum(t => t.Miles);
                decimal totalAt45   = tripList.Sum(t => t.MilesAt45p);
                decimal totalAt25   = tripList.Sum(t => t.MilesAt25p);
                decimal totalAmount = tripList.Sum(t => t.TotalAmount);

                var (rate45p, rate25p, threshold) = await GetAmapSettingsAsync();
                var summary = new
                {
                    TaxYear         = taxYear,
                    Director        = director,
                    TotalMiles      = totalMiles,
                    MilesAt45p      = totalAt45,
                    MilesAt25p      = totalAt25,
                    TotalAmount     = totalAmount,
                    ThresholdMiles  = threshold,
                    Remaining45pMiles = Math.Max(0, threshold - totalMiles),
                    CurrentRate     = totalMiles >= threshold ? $"{rate25p * 100:0}p" : $"{rate45p * 100:0}p",
                    Rate45p         = rate45p,
                    Rate25p         = rate25p,
                    TripCount       = tripList.Count,
                    DraftCount      = tripList.Count(t => t.Status == "Draft"),
                    ClaimedCount    = tripList.Count(t => t.Status == "Claimed"),
                    PaidCount       = tripList.Count(t => t.Status == "Paid")
                };

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(summary);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mileage summary");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // GET /api/mileage/claims
        // ─────────────────────────────────────────────────────────────────
        [Function("GetMileageClaims")]
        public async Task<HttpResponseData> GetMileageClaims(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mileage/claims")] HttpRequestData req)
        {
            _logger.LogInformation("GetMileageClaims called");
            try
            {
                var qs       = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var director = qs["director"];
                var taxYear  = qs["taxYear"];

                IEnumerable<MileageClaim> claims;
                if (!string.IsNullOrEmpty(director) && !string.IsNullOrEmpty(taxYear))
                    claims = await _claimRepository.GetByDirectorAndTaxYearAsync(director, taxYear);
                else if (!string.IsNullOrEmpty(taxYear))
                    claims = await _claimRepository.GetByTaxYearAsync(taxYear);
                else if (!string.IsNullOrEmpty(director))
                    claims = await _claimRepository.GetByDirectorAsync(director);
                else
                    claims = await _claimRepository.GetAllAsync();

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(claims);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mileage claims");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/mileage/claims/generate
        // Body: { director, periodStart, periodEnd, notes? }
        // Bundles all unclaimed Draft trips in the period into a new claim
        // ─────────────────────────────────────────────────────────────────
        [Function("GenerateMileageClaim")]
        public async Task<HttpResponseData> GenerateMileageClaim(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mileage/claims/generate")] HttpRequestData req)
        {
            _logger.LogInformation("GenerateMileageClaim called");
            try
            {
                var body = await req.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body!);
                var root = doc.RootElement;

                var director    = root.GetProperty("director").GetString() ?? string.Empty;
                var periodStart = root.GetProperty("periodStart").GetDateTime();
                var periodEnd   = root.GetProperty("periodEnd").GetDateTime();
                var notes       = root.TryGetProperty("notes", out var n) ? n.GetString() : null;

                // Get all Draft trips for this director in the date range (not already in a claim)
                var draftTrips = (await _tripRepository.GetDraftTripsByDirectorAsync(director))
                    .Where(t => t.TripDate >= periodStart && t.TripDate <= periodEnd && t.ClaimId == null)
                    .ToList();

                if (draftTrips.Count == 0)
                {
                    var empty = req.CreateResponse(HttpStatusCode.BadRequest);
                    await empty.WriteStringAsync("No unclaimed Draft trips found in this period");
                    return empty;
                }

                var taxYear = GetTaxYear(periodStart);

                var claim = new MileageClaim
                {
                    ClaimRef    = await _claimRepository.GenerateNextClaimRefAsync(),
                    Director    = director,
                    PeriodStart = periodStart,
                    PeriodEnd   = periodEnd,
                    TaxYear     = taxYear,
                    TotalMiles  = draftTrips.Sum(t => t.Miles),
                    MilesAt45p  = draftTrips.Sum(t => t.MilesAt45p),
                    MilesAt25p  = draftTrips.Sum(t => t.MilesAt25p),
                    TotalAmount = draftTrips.Sum(t => t.TotalAmount),
                    Status      = "Draft",
                    Notes       = notes,
                    CreatedAt   = DateTime.UtcNow
                };

                var createdClaim = await _claimRepository.CreateAsync(claim);

                // Link trips to this claim
                foreach (var trip in draftTrips)
                {
                    trip.ClaimId = createdClaim.Id;
                    await _tripRepository.UpdateAsync(trip);
                }

                var resp = req.CreateResponse(HttpStatusCode.Created);
                await resp.WriteAsJsonAsync(createdClaim);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating mileage claim");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/mileage/claims/{id}/submit
        // Creates DLA entry (Dr Mileage Expense / Cr DLA) and locks trips
        // ─────────────────────────────────────────────────────────────────
        [Function("SubmitMileageClaim")]
        public async Task<HttpResponseData> SubmitMileageClaim(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mileage/claims/{id:int}/submit")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation("SubmitMileageClaim called for claim {Id}", id);
            try
            {
                var claim = await _claimRepository.GetByIdAsync(id);
                if (claim == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Claim not found");
                    return notFound;
                }
                if (claim.Status != "Draft")
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync($"Claim is already {claim.Status}");
                    return conflict;
                }

                // Get trips linked to this claim
                var trips = (await _tripRepository.GetAllAsync())
                    .Where(t => t.ClaimId == id && t.Status == "Draft")
                    .ToList();

                if (trips.Count == 0)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("No Draft trips found for this claim");
                    return bad;
                }

                // Recalculate totals from current trips (in case any were edited)
                claim.TotalMiles  = trips.Sum(t => t.Miles);
                claim.MilesAt45p  = trips.Sum(t => t.MilesAt45p);
                claim.MilesAt25p  = trips.Sum(t => t.MilesAt25p);
                claim.TotalAmount = trips.Sum(t => t.TotalAmount);

                var periodKey   = claim.PeriodEnd.ToString("yyyy-MM");
                var description = $"Mileage Allowance — {claim.Director} — {claim.PeriodStart:dd MMM yyyy} to {claim.PeriodEnd:dd MMM yyyy} " +
                                  $"({claim.TotalMiles:0.##} miles @ {claim.MilesAt45p:0.##}mi×45p + {claim.MilesAt25p:0.##}mi×25p)";

                // Create DLA entry (Director owed reimbursement)
                var dlaEntry = new DlaEntry
                {
                    Director      = claim.Director,
                    Direction     = "OwedToDirector",
                    EntryDate     = claim.PeriodEnd,
                    Description   = description,
                    Category      = "Mileage Allowance",
                    CtTag         = "Revenue",
                    AmountNet     = claim.TotalAmount,
                    VatAmount     = 0m,
                    AmountGross   = claim.TotalAmount,
                    AmountPaid    = 0m,
                    RemainingBalance = claim.TotalAmount,
                    PeriodKey     = periodKey,
                    TaxYear       = claim.TaxYear,
                    FinancialYear = claim.PeriodEnd.Year.ToString(),
                    CreatedDate   = DateTime.UtcNow,
                    ModifiedDate  = DateTime.UtcNow
                };
                dlaEntry.DlaId = await _dlaRepository.GenerateNextDlaIdAsync();
                var createdDla = await _dlaRepository.CreateAsync(dlaEntry);

                // Company ledger entry
                var ledgerEntry = new CompanyLedgerEntry
                {
                    Title         = $"Mileage Claim: {claim.ClaimRef} — {claim.Director}",
                    EntryType     = "DLA_Out",
                    Amount        = claim.TotalAmount,
                    EffectiveDate = claim.PeriodEnd,
                    Notes         = $"DLA ID: {createdDla.DlaId}. Claim: {claim.ClaimRef}. {claim.TotalMiles:0.##} miles. CT Tag: Revenue",
                    PeriodKey     = periodKey,
                    TaxYear       = claim.PeriodEnd.Year,
                    FinancialYear = claim.PeriodEnd.Year.ToString()
                };
                await _ledgerRepository.CreateAsync(ledgerEntry);

                // Lock the trips
                foreach (var trip in trips)
                {
                    trip.Status = "Claimed";
                    await _tripRepository.UpdateAsync(trip);
                }

                // Update claim
                claim.Status      = "Posted";
                claim.SubmittedAt = DateTime.UtcNow;
                claim.PostedAt    = DateTime.UtcNow;
                claim.DlaEntryId  = createdDla.Id;
                await _claimRepository.UpdateAsync(claim);

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(new
                {
                    claim       = claim,
                    dlaEntry    = createdDla,
                    tripsLocked = trips.Count
                });
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting mileage claim");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/mileage/claims/{id}/paid
        // Marks the director as reimbursed; optionally records the bank payment
        // ─────────────────────────────────────────────────────────────────
        [Function("MarkMileageClaimPaid")]
        public async Task<HttpResponseData> MarkMileageClaimPaid(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mileage/claims/{id:int}/paid")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation("MarkMileageClaimPaid called for claim {Id}", id);
            try
            {
                var claim = await _claimRepository.GetByIdAsync(id);
                if (claim == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Claim not found");
                    return notFound;
                }
                if (claim.Status != "Posted")
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync("Only Posted claims can be marked as Paid");
                    return conflict;
                }

                // Mark the original DLA entry as paid
                if (claim.DlaEntryId.HasValue)
                {
                    var dlaEntry = await _dlaRepository.GetByIdAsync(claim.DlaEntryId.Value);
                    if (dlaEntry != null)
                    {
                        dlaEntry.AmountPaid       = dlaEntry.AmountGross;
                        dlaEntry.RemainingBalance = 0m;
                        dlaEntry.DatePaid         = DateTime.UtcNow;
                        dlaEntry.PaymentMethod    = "Bank Transfer";
                        dlaEntry.ModifiedDate     = DateTime.UtcNow;
                        await _dlaRepository.UpdateAsync(dlaEntry);
                    }
                }

                // Mark trips as Paid
                var trips = (await _tripRepository.GetAllAsync())
                    .Where(t => t.ClaimId == id)
                    .ToList();
                foreach (var trip in trips)
                {
                    trip.Status = "Paid";
                    await _tripRepository.UpdateAsync(trip);
                }

                claim.Status = "Paid";
                claim.PaidAt = DateTime.UtcNow;
                await _claimRepository.UpdateAsync(claim);

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(claim);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking mileage claim paid");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
        }
    }
}
