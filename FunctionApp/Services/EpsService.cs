using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;

namespace FinanceHubFunctions.Services
{
    /// <summary>
    /// Builds and submits HMRC PAYE RTI Employer Payment Summary (EPS) via the
    /// Government Gateway GovTalk XML transaction engine.
    ///
    /// Credentials are sourced exclusively from:
    ///   - Gateway User ID  → CompanySettings.HmrcGatewayUserId  (database)
    ///   - Gateway Password → Azure Key Vault secret "HmrcGatewayPassword"
    /// Both are configured via Settings → HMRC in the UI. Nothing is hardcoded.
    /// </summary>
    public class EpsService
    {
        private readonly HttpClient _http;
        private readonly KeyVaultService _keyVault;
        private readonly FinanceHubDbContext _db;

        private const string SOFTWARE_NAME    = "FinanceHub";
        private const string SOFTWARE_VERSION = "1.0";
        private const string URL = "https://transaction-engine.tax.service.gov.uk/submission";

        public EpsService(IHttpClientFactory httpClientFactory, KeyVaultService keyVault, FinanceHubDbContext db)
        {
            _http     = httpClientFactory.CreateClient("hmrc");
            _keyVault = keyVault;
            _db       = db;
        }

        /// <summary>
        /// Submits an EPS for a "no payment" period — tells HMRC no employees were paid
        /// in the given tax month range.
        /// </summary>
        public Task<FpsSubmissionResult> SubmitNoPaymentEpsAsync(
            string taxYear, int fromTaxMonth, int toTaxMonth,
            PayrollSettings settings)
        {
            var (taxYearStart, taxYearEnd) = ParseTaxYear(taxYear);

            // "No payment" date range within the tax year
            var periodFrom = TaxMonthStartDate(fromTaxMonth, taxYearStart);
            var periodTo   = TaxMonthEndDate(toTaxMonth, taxYearStart);

            var body = new XElement[] {
                new XElement("NoPaymentForPeriod",
                    new XElement("From", periodFrom.ToString("yyyy-MM-dd")),
                    new XElement("To",   periodTo.ToString("yyyy-MM-dd"))
                )
            };

            return SubmitEpsAsync(settings, body, taxYear, $"No payment EPS ({taxYear} months {fromTaxMonth}–{toTaxMonth})");
        }

        /// <summary>
        /// Submits a year-end EPS — the final submission flag after the last FPS of the tax year.
        /// Must be submitted between the last FPS and 19 April.
        /// </summary>
        public Task<FpsSubmissionResult> SubmitYearEndEpsAsync(string taxYear, PayrollSettings settings)
        {
            var (taxYearStart, _) = ParseTaxYear(taxYear);

            // The year-end date is always 5 April of the closing year
            var yearEndDate = new DateTime(taxYearStart + 1, 4, 5);

            var body = new XElement[] {
                new XElement("FinalSubmission",
                    new XElement("HMRCMarkAsAccepted", "yes"),
                    new XElement("DateEmpCeasedPaying", yearEndDate.ToString("yyyy-MM-dd"))
                )
            };

            return SubmitEpsAsync(settings, body, taxYear, $"Year-end EPS ({taxYear})");
        }

        // ── Core submitter ────────────────────────────────────────────────────

        private async Task<FpsSubmissionResult> SubmitEpsAsync(
            PayrollSettings settings,
            XElement[] epsBodyElements,
            string taxYear,
            string logLabel)
        {
            // Fetch credentials at submission time — User ID from DB, password from Key Vault.
            var companySettings = await _db.CompanySettings.FirstOrDefaultAsync();
            var gatewayUserId   = companySettings?.HmrcGatewayUserId ?? "";
            var gatewayPassword = await _keyVault.GetHmrcGatewayPasswordAsync() ?? "";

            if (string.IsNullOrWhiteSpace(gatewayUserId) || string.IsNullOrWhiteSpace(gatewayPassword))
                return new FpsSubmissionResult(false, null,
                    "Gateway credentials not configured. " +
                    "Enter your Government Gateway User ID and Password in Settings → HMRC.", null);

            string xml;
            try { xml = BuildGovTalkXml(settings, epsBodyElements, gatewayUserId, gatewayPassword); }
            catch (Exception ex)
            {
                return new FpsSubmissionResult(false, null, $"XML build error: {ex.Message}", null);
            }

            try
            {
                using var content = new StringContent(xml, Encoding.UTF8, "text/xml");
                var response = await _http.PostAsync(URL, content);
                var body     = await response.Content.ReadAsStringAsync();
                return ParseGovTalkResponse(body, response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                return new FpsSubmissionResult(false, null, $"HTTP submission error: {ex.Message}", null);
            }
        }

        // ── XML Builder ───────────────────────────────────────────────────────

        private string BuildGovTalkXml(PayrollSettings settings, XElement[] epsBodyElements, string gatewayUserId, string gatewayPassword)
        {
            // Parse PAYE ref "123/AB12345" → officeNum + payeRef
            var refParts  = (settings.EmployerPAYEReference ?? "").Split('/', 2);
            var officeNum = refParts.Length > 0 ? refParts[0].Trim() : "";
            var payeRef   = refParts.Length > 1 ? refParts[1].Trim() : "";
            var aoRef     = (settings.AccountsOfficeReference ?? "").Trim();

            var transId = Guid.NewGuid().ToString("N")[..20].ToUpper();

            var gtkNs = XNamespace.Get("http://www.govtalk.gov.uk/CM/envelope");
            var epsNs = XNamespace.Get("http://www.govtalk.gov.uk/taxation/PAYE/RTI/EmployerPaymentSummary/2017-01-06");
            var xsiNs = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

            // Build EPS body elements in correct namespace
            var nsBodyElements = epsBodyElements
                .Select(el => InNamespace(el, epsNs))
                .ToArray();

            var epsRoot = new XElement(epsNs + "EmployerPaymentSummary",
                new XAttribute("xmlns",                   epsNs),
                new XAttribute(XNamespace.Xmlns + "xsi", xsiNs),
                new XElement(epsNs + "EmpRefs",
                    new XElement(epsNs + "OfficeNum", officeNum),
                    new XElement(epsNs + "PayeRef",   payeRef),
                    new XElement(epsNs + "AORef",     aoRef)
                ),
                nsBodyElements
            );

            var govTalk = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(gtkNs + "GovTalkMessage",
                    new XAttribute("xmlns",                   gtkNs),
                    new XAttribute(XNamespace.Xmlns + "xsi", xsiNs),
                    new XElement(gtkNs + "EnvelopeVersion", "2.0"),
                    new XElement(gtkNs + "Header",
                        new XElement(gtkNs + "MessageDetails",
                            new XElement(gtkNs + "Class",         "HMRC-PAYE-RTI-EPS"),
                            new XElement(gtkNs + "Qualifier",     "request"),
                            new XElement(gtkNs + "Function",      "submit"),
                            new XElement(gtkNs + "TransactionID", transId),
                            new XElement(gtkNs + "CorrelationID"),
                            new XElement(gtkNs + "Transformation","XML")
                        ),
                        new XElement(gtkNs + "SenderDetails",
                            new XElement(gtkNs + "IDAuthentication",
                                new XElement(gtkNs + "SenderID", gatewayUserId),
                                new XElement(gtkNs + "Authentication",
                                    new XElement(gtkNs + "Method", "clear"),
                                    new XElement(gtkNs + "Role",   "principal"),
                                    new XElement(gtkNs + "Value",  gatewayPassword)
                                )
                            )
                        )
                    ),
                    new XElement(gtkNs + "GovTalkDetails",
                        new XElement(gtkNs + "Keys",
                            new XElement(gtkNs + "Key",
                                new XAttribute("Type", "TaxOfficeNumber"),
                                officeNum),
                            new XElement(gtkNs + "Key",
                                new XAttribute("Type", "TaxOfficeReference"),
                                payeRef)
                        ),
                        new XElement(gtkNs + "ChannelRouting",
                            new XElement(gtkNs + "Channel",
                                new XElement(gtkNs + "URI",     "974"),
                                new XElement(gtkNs + "Product", SOFTWARE_NAME),
                                new XElement(gtkNs + "Version", SOFTWARE_VERSION)
                            )
                        )
                    ),
                    new XElement(gtkNs + "Body", epsRoot)
                )
            );

            return govTalk.Declaration + "\n" + govTalk.Root?.ToString(SaveOptions.None);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Recursively re-namespaces an XElement tree.</summary>
        private static XElement InNamespace(XElement el, XNamespace ns)
        {
            return new XElement(ns + el.Name.LocalName,
                el.Attributes(),
                el.Nodes().Select(n =>
                    n is XElement child ? InNamespace(child, ns) : n));
        }

        private static (int start, int end) ParseTaxYear(string taxYear)
        {
            // "2025-26" → (2025, 2026)
            var parts = (taxYear ?? "").Split('-');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int s)
                && int.TryParse(parts[1], out int e))
            {
                return (s, e < 100 ? 2000 + e : e);
            }
            int y = DateTime.Now.Year;
            return (y, y + 1);
        }

        /// <summary>Returns the start date of HMRC tax month N within a tax year starting in April of taxYearStart.</summary>
        private static DateTime TaxMonthStartDate(int taxMonth, int taxYearStart)
        {
            // Tax month 1 = 6 April, month 2 = 6 May, … month 12 = 6 March
            int calMonth = ((taxMonth - 1 + 3) % 12) + 1; // April=1→4, May=2→5, … March=12→3
            int year     = calMonth >= 4 ? taxYearStart : taxYearStart + 1;
            return new DateTime(year, calMonth, 6);
        }

        /// <summary>Returns the end date of HMRC tax month N (= 5th of the following calendar month).</summary>
        private static DateTime TaxMonthEndDate(int taxMonth, int taxYearStart)
        {
            var start = TaxMonthStartDate(taxMonth, taxYearStart);
            return start.AddMonths(1).AddDays(-1); // 5th of following month
        }

        // ── Response parser (same logic as FpsService) ────────────────────────

        private static FpsSubmissionResult ParseGovTalkResponse(string rawXml, bool httpSuccess)
        {
            if (string.IsNullOrWhiteSpace(rawXml))
                return new FpsSubmissionResult(httpSuccess, null,
                    httpSuccess ? "Submitted (no response body)" : "Empty response from HMRC", rawXml);

            try
            {
                var doc    = XDocument.Parse(rawXml);
                var ns     = XNamespace.Get("http://www.govtalk.gov.uk/CM/envelope");
                var msgDet = doc.Descendants(ns + "MessageDetails").FirstOrDefault();
                var qual   = msgDet?.Element(ns + "Qualifier")?.Value;
                var corrId = msgDet?.Element(ns + "CorrelationID")?.Value;

                var errors = doc.Descendants(ns + "Error").ToList();
                if (errors.Count > 0)
                {
                    var msgs = string.Join("; ", errors.Select(err =>
                        err.Element(ns + "Text")?.Value ?? err.ToString()));
                    return new FpsSubmissionResult(false, corrId, $"HMRC error: {msgs}", rawXml);
                }

                bool success = qual is "acknowledgement" or "response" || httpSuccess;
                var message  = qual switch
                {
                    "acknowledgement" => "EPS accepted by HMRC ✓",
                    "response"        => "EPS response received from HMRC",
                    _                 => $"HMRC qualifier: {qual ?? "unknown"}"
                };
                return new FpsSubmissionResult(success, corrId, message, rawXml);
            }
            catch
            {
                var preview = rawXml.Length > 300 ? rawXml[..300] + "…" : rawXml;
                return new FpsSubmissionResult(httpSuccess, null, $"Raw response: {preview}", rawXml);
            }
        }
    }
}
