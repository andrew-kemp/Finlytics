using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class MonzoFunctions
    {
        private readonly ILogger<MonzoFunctions> _logger;
        private readonly IBankAccountRepository _bankAccountRepository;
        private readonly IBankTransactionRepository _bankTransactionRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        // Monzo pence → pounds
        private static decimal PenceToPounds(long pence) => pence / 100m;

        private const string RedirectUri = "https://financehub-func-kemponline.azurewebsites.net/api/monzo/callback";
        private const string FrontendBankingUrl = "https://finhub.andykemp.cloud/banking";

        // ── GET /api/monzo/auth ───────────────────────────────────────────────────
        // Returns the Monzo OAuth URL for the frontend to redirect to
        [Function("MonzoStartAuth")]
        public async Task<HttpResponseData> StartAuth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "monzo/auth")] HttpRequestData req)
        {
            var clientId = Environment.GetEnvironmentVariable("MonzoClientId");
            if (string.IsNullOrEmpty(clientId))
            {
                var err = req.CreateResponse(HttpStatusCode.BadRequest);
                await err.WriteAsJsonAsync(new { error = "Monzo OAuth client not configured" });
                return err;
            }
            var state = Guid.NewGuid().ToString("N");
            var authUrl = $"https://auth.monzo.com/?client_id={clientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&state={state}";
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { authUrl });
            return response;
        }

        // ── GET /api/monzo/callback ───────────────────────────────────────────────
        // Monzo redirects here after user approves — exchanges code for token
        [Function("MonzoCallback")]
        public async Task<HttpResponseData> HandleCallback(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "monzo/callback")] HttpRequestData req)
        {
            try
            {
                var query = QueryHelpers.ParseQuery(req.Url.Query);
                var code = query.TryGetValue("code", out var c) ? c.ToString() : null;
                var error = query.TryGetValue("error", out var e) ? e.ToString() : null;

                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
                {
                    var errResp = req.CreateResponse(HttpStatusCode.Redirect);
                    errResp.Headers.Add("Location", $"{FrontendBankingUrl}?monzo_error={Uri.EscapeDataString(error ?? "cancelled")}");
                    return errResp;
                }

                var clientId = Environment.GetEnvironmentVariable("MonzoClientId");
                var clientSecret = Environment.GetEnvironmentVariable("MonzoClientSecret");

                // Exchange authorisation code for access token
                var client = _httpClientFactory.CreateClient();
                var tokenResp = await client.PostAsync("https://api.monzo.com/oauth2/token",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"]    = "authorization_code",
                        ["client_id"]     = clientId!,
                        ["client_secret"] = clientSecret!,
                        ["redirect_uri"]  = RedirectUri,
                        ["code"]          = code
                    }));

                if (!tokenResp.IsSuccessStatusCode)
                {
                    var detail = await tokenResp.Content.ReadAsStringAsync();
                    _logger.LogError("Monzo token exchange failed: {Status} {Detail}", tokenResp.StatusCode, detail);
                    var errResp = req.CreateResponse(HttpStatusCode.Redirect);
                    errResp.Headers.Add("Location", $"{FrontendBankingUrl}?monzo_error={Uri.EscapeDataString($"Token exchange failed ({tokenResp.StatusCode}): {detail}")}");
                    return errResp;
                }

                var tokenJson = await tokenResp.Content.ReadAsStringAsync();
                using var tokenDoc = JsonDocument.Parse(tokenJson);
                var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;
                var refreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

                // Get the business account ID
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var accountsResp = await client.GetAsync("https://api.monzo.com/accounts?account_type=uk_business");
                var accountsJson = await accountsResp.Content.ReadAsStringAsync();
                using var accountsDoc = JsonDocument.Parse(accountsJson);

                string? accountId = null;
                if (accountsDoc.RootElement.TryGetProperty("accounts", out var accs))
                {
                    foreach (var acc in accs.EnumerateArray())
                    {
                        var closed = acc.TryGetProperty("closed", out var cl) && cl.GetBoolean();
                        if (!closed && acc.TryGetProperty("id", out var aid))
                        {
                            accountId = aid.GetString();
                            break;
                        }
                    }
                }

                // Persist to Key Vault so the next app restart picks it up
                var keyVaultUri = Environment.GetEnvironmentVariable("KEY_VAULT_URL")
                    ?? Environment.GetEnvironmentVariable("KeyVaultUri");
                if (!string.IsNullOrEmpty(keyVaultUri))
                {
                    var credential = new DefaultAzureCredential();
                    var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
                    await secretClient.SetSecretAsync("MonzoAccessToken", accessToken);
                    if (!string.IsNullOrEmpty(refreshToken))
                        await secretClient.SetSecretAsync("MonzoRefreshToken", refreshToken);
                    if (!string.IsNullOrEmpty(accountId))
                        await secretClient.SetSecretAsync("MonzoAccountId", accountId);
                }

                // Update in-process env vars immediately (no restart needed)
                Environment.SetEnvironmentVariable("MonzoAccessToken", accessToken);
                if (!string.IsNullOrEmpty(accountId))
                    Environment.SetEnvironmentVariable("MonzoAccountId", accountId);

                var response = req.CreateResponse(HttpStatusCode.Redirect);
                response.Headers.Add("Location", $"{FrontendBankingUrl}?monzo_connected=true");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monzo OAuth callback error");
                var errResp = req.CreateResponse(HttpStatusCode.Redirect);
                errResp.Headers.Add("Location", $"{FrontendBankingUrl}?monzo_error={Uri.EscapeDataString(ex.Message)}");
                return errResp;
            }
        }

        public MonzoFunctions(
            ILogger<MonzoFunctions> logger,
            IBankAccountRepository bankAccountRepository,
            IBankTransactionRepository bankTransactionRepository,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _bankAccountRepository = bankAccountRepository;
            _bankTransactionRepository = bankTransactionRepository;
            _httpClientFactory = httpClientFactory;
        }

        // ── GET /api/monzo/status ─────────────────────────────────────────────────
        [Function("MonzoGetStatus")]
        public async Task<HttpResponseData> GetStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "monzo/status")] HttpRequestData req)
        {
            try
            {
                var token = Environment.GetEnvironmentVariable("MonzoAccessToken");
                var accountId = Environment.GetEnvironmentVariable("MonzoAccountId");

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(accountId))
                {
                    var notConnected = req.CreateResponse(HttpStatusCode.OK);
                    await notConnected.WriteAsJsonAsync(new { connected = false, message = "Not connected" });
                    return notConnected;
                }

                // Ping Monzo to check token is still valid
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var pingResp = await client.GetAsync($"https://api.monzo.com/balance?account_id={accountId}");

                if (!pingResp.IsSuccessStatusCode)
                {
                    var expired = req.CreateResponse(HttpStatusCode.OK);
                    await expired.WriteAsJsonAsync(new { connected = false, message = "Token expired — please reconnect", tokenExpired = true });
                    return expired;
                }

                var balJson = await pingResp.Content.ReadAsStringAsync();
                using var balDoc = JsonDocument.Parse(balJson);
                var balRoot = balDoc.RootElement;

                var balance = balRoot.TryGetProperty("balance", out var b) ? b.GetInt64() : 0L;
                var totalBalance = balRoot.TryGetProperty("total_balance", out var tb) ? tb.GetInt64() : 0L;
                var currency = balRoot.TryGetProperty("currency", out var c) ? c.GetString() : "GBP";
                var spendToday = balRoot.TryGetProperty("spend_today", out var st) ? st.GetInt64() : 0L;

                // Get last sync time from bank account record
                var accounts = await _bankAccountRepository.GetAllAsync();
                var monzoAccount = accounts?.FirstOrDefault(a => a.MonzoConnected && !string.IsNullOrEmpty(a.MonzoAccountId));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    connected = true,
                    accountId,
                    balance = PenceToPounds(balance),
                    totalBalance = PenceToPounds(totalBalance),
                    spendToday = PenceToPounds(Math.Abs(spendToday)),
                    currency,
                    lastSyncedAt = monzoAccount?.MonzoLastSyncedAt,
                    bankAccountId = monzoAccount?.Id
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Monzo status");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ── POST /api/monzo/sync ──────────────────────────────────────────────────
        // Imports transactions from Monzo into BankTransactions table
        [Function("MonzoSync")]
        public async Task<HttpResponseData> SyncTransactions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "monzo/sync")] HttpRequestData req)
        {
            try
            {
                var token = Environment.GetEnvironmentVariable("MonzoAccessToken");
                var accountId = Environment.GetEnvironmentVariable("MonzoAccountId");

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(accountId))
                {
                    var notConn = req.CreateResponse(HttpStatusCode.BadRequest);
                    await notConn.WriteAsJsonAsync(new { error = "Monzo not connected" });
                    return notConn;
                }

                // Parse optional since/before from body
                DateTime since = DateTime.UtcNow.AddDays(-90); // default: last 90 days
                DateTime? before = null;
                try
                {
                    var body = await new StreamReader(req.Body).ReadToEndAsync();
                    if (!string.IsNullOrEmpty(body))
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("since", out var s) && DateTime.TryParse(s.GetString(), out var sd))
                            since = sd;
                        if (doc.RootElement.TryGetProperty("before", out var bf) && DateTime.TryParse(bf.GetString(), out var bd))
                            before = bd;
                    }
                }
                catch { /* use defaults */ }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"https://api.monzo.com/transactions?account_id={accountId}&expand[]=merchant&since={since:yyyy-MM-ddTHH:mm:ssZ}&limit=100";
                if (before.HasValue)
                    url += $"&before={before.Value:yyyy-MM-ddTHH:mm:ssZ}";

                var txResp = await client.GetAsync(url);
                if (!txResp.IsSuccessStatusCode)
                {
                    var errBody = await txResp.Content.ReadAsStringAsync();
                    _logger.LogError("Monzo API error: {Status} {Body}", txResp.StatusCode, errBody);
                    var apiErr = req.CreateResponse(HttpStatusCode.BadGateway);
                    await apiErr.WriteAsJsonAsync(new { error = "Monzo API error", detail = errBody });
                    return apiErr;
                }

                var txJson = await txResp.Content.ReadAsStringAsync();
                using var txDoc = JsonDocument.Parse(txJson);

                // Find or create the FinanceHub bank account record for Monzo
                var allAccounts = await _bankAccountRepository.GetAllAsync();
                var monzoAccount = allAccounts?.FirstOrDefault(a => a.MonzoConnected)
                    ?? allAccounts?.FirstOrDefault(a => a.BankName?.ToLower().Contains("monzo") == true);

                if (monzoAccount == null)
                {
                    // Auto-create the bank account record
                    monzoAccount = await _bankAccountRepository.CreateAsync(new BankAccount
                    {
                        AccountName = "ANDY KEMP CONSULTING LTD",
                        BankName = "Monzo",
                        Currency = "GBP",
                        IsActive = true,
                        MonzoAccountId = accountId,
                        MonzoConnected = true,
                        CreatedDate = DateTime.UtcNow
                    });
                }
                else if (!monzoAccount.MonzoConnected || string.IsNullOrEmpty(monzoAccount.MonzoAccountId))
                {
                    monzoAccount.MonzoAccountId = accountId;
                    monzoAccount.MonzoConnected = true;
                    await _bankAccountRepository.UpdateAsync(monzoAccount);
                }

                // Get existing Monzo transaction IDs to avoid duplicates
                var existingTxs = await _bankTransactionRepository.GetByAccountIdAsync(monzoAccount.Id);
                var existingMonzoIds = new HashSet<string>(
                    existingTxs?.Where(t => !string.IsNullOrEmpty(t.MonzoTransactionId))
                               .Select(t => t.MonzoTransactionId!) ?? Enumerable.Empty<string>()
                );

                int imported = 0, skipped = 0;

                if (txDoc.RootElement.TryGetProperty("transactions", out var transactions))
                {
                    foreach (var tx in transactions.EnumerateArray())
                    {
                        var monzoTxId = tx.TryGetProperty("id", out var tid) ? tid.GetString() : null;
                        if (string.IsNullOrEmpty(monzoTxId)) continue;

                        // Skip if already imported
                        if (existingMonzoIds.Contains(monzoTxId)) { skipped++; continue; }

                        var amountPence = tx.TryGetProperty("amount", out var amt) ? amt.GetInt64() : 0L;

                        // Skip zero-amount and pending declined transactions
                        if (amountPence == 0) { skipped++; continue; }

                        var created = tx.TryGetProperty("created", out var cr) && DateTime.TryParse(cr.GetString(), out var crd) ? crd : DateTime.UtcNow;
                        var description = tx.TryGetProperty("description", out var desc) ? desc.GetString() : "";
                        var notes = tx.TryGetProperty("notes", out var n) ? n.GetString() : null;

                        // Merchant name (if expanded)
                        string? merchantName = null;
                        if (tx.TryGetProperty("merchant", out var merchant) && merchant.ValueKind == JsonValueKind.Object)
                        {
                            if (merchant.TryGetProperty("name", out var mn)) merchantName = mn.GetString();
                        }

                        var monzoCategory = tx.TryGetProperty("category", out var cat) ? cat.GetString() : null;

                        // Map Monzo category to FinanceHub category
                        var category = MapMonzoCategory(monzoCategory, amountPence);

                        // Reference from metadata
                        string? reference = null;
                        if (tx.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
                        {
                            if (meta.TryGetProperty("faster_payment", out var fp)) reference = fp.GetString();
                            else if (meta.TryGetProperty("reference", out var rf)) reference = rf.GetString();
                        }

                        var displayName = !string.IsNullOrEmpty(merchantName) ? merchantName : description;

                        await _bankTransactionRepository.CreateAsync(new BankTransaction
                        {
                            BankAccountId = monzoAccount.Id,
                            TransactionDate = created,
                            Amount = Math.Abs(PenceToPounds(amountPence)),
                            Direction = amountPence > 0 ? "In" : "Out",
                            Description = displayName,
                            Reference = reference,
                            Category = category,
                            Source = "Monzo",
                            ExternalId = monzoTxId,
                            MonzoTransactionId = monzoTxId,
                            MonzoMerchantName = merchantName,
                            MonzoCategory = monzoCategory,
                            MonzoNotes = notes,
                            IsReconciled = false,
                            CreatedDate = DateTime.UtcNow
                        });
                        imported++;
                    }
                }

                // Update last synced time
                monzoAccount.MonzoLastSyncedAt = DateTime.UtcNow;
                await _bankAccountRepository.UpdateAsync(monzoAccount);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    imported,
                    skipped,
                    message = $"Sync complete — {imported} new transaction{(imported == 1 ? "" : "s")} imported",
                    lastSyncedAt = monzoAccount.MonzoLastSyncedAt,
                    bankAccountId = monzoAccount.Id
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Monzo transactions");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ── GET /api/monzo/balance ────────────────────────────────────────────────
        [Function("MonzoGetBalance")]
        public async Task<HttpResponseData> GetBalance(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "monzo/balance")] HttpRequestData req)
        {
            try
            {
                var token = Environment.GetEnvironmentVariable("MonzoAccessToken");
                var accountId = Environment.GetEnvironmentVariable("MonzoAccountId");

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(accountId))
                {
                    var notConn = req.CreateResponse(HttpStatusCode.BadRequest);
                    await notConn.WriteAsJsonAsync(new { error = "Monzo not connected" });
                    return notConn;
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var balResp = await client.GetAsync($"https://api.monzo.com/balance?account_id={accountId}");
                var balJson = await balResp.Content.ReadAsStringAsync();
                using var balDoc = JsonDocument.Parse(balJson);
                var root = balDoc.RootElement;

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    balance = PenceToPounds(root.TryGetProperty("balance", out var b) ? b.GetInt64() : 0),
                    totalBalance = PenceToPounds(root.TryGetProperty("total_balance", out var tb) ? tb.GetInt64() : 0),
                    spendToday = PenceToPounds(Math.Abs(root.TryGetProperty("spend_today", out var st) ? st.GetInt64() : 0)),
                    currency = root.TryGetProperty("currency", out var c) ? c.GetString() : "GBP"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Monzo balance");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        private static string MapMonzoCategory(string? monzoCategory, long amountPence)
        {
            if (amountPence > 0) return "Income";
            return monzoCategory switch
            {
                "transport"     => "Travel",
                "eating_out"    => "Meals",
                "entertainment" => "Entertainment",
                "bills"         => "Utilities",
                "shopping"      => "Office Supplies",
                "personal_care" => "Personal",
                "expenses"      => "Expenses",
                "transfers"     => "Transfer",
                "general"       => "General",
                _               => "Other"
            };
        }
    }
}
