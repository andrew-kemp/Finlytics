using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Helpers;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinanceHubFunctions.Functions
{
    public class ShareholderFunctions
    {
        private readonly ILogger<ShareholderFunctions> _logger;
        private readonly IShareholderRepository _shareholderRepository;
        private readonly ICompanySettingsRepository _companySettingsRepository;
        private readonly EmailService _emailService;
        private readonly BlobStorageService _blobStorageService;
        private readonly DeletionGuardService _guard;

        public ShareholderFunctions(
            ILogger<ShareholderFunctions> logger,
            IShareholderRepository shareholderRepository,
            ICompanySettingsRepository companySettingsRepository,
            EmailService emailService,
            BlobStorageService blobStorageService,
            DeletionGuardService guard)
        {
            _logger = logger;
            _shareholderRepository = shareholderRepository;
            _companySettingsRepository = companySettingsRepository;
            _emailService = emailService;
            _blobStorageService = blobStorageService;
            _guard = guard;
        }

        [Function("GetShareholders")]
        public async Task<HttpResponseData> GetShareholders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shareholders")] HttpRequestData req)
        {
            _logger.LogInformation("GetShareholders function triggered");

            try
            {
                var shareholders = await _shareholderRepository.GetAllAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(shareholders);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shareholders");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetShareholderById")]
        public async Task<HttpResponseData> GetShareholderById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shareholders/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"GetShareholderById function triggered for ID: {id}");

            try
            {
                var shareholder = await _shareholderRepository.GetByIdAsync(id);
                if (shareholder == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Shareholder not found");
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(shareholder);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting shareholder {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("CreateShareholder")]
        public async Task<HttpResponseData> CreateShareholder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shareholders")] HttpRequestData req)
        {
            _logger.LogInformation("CreateShareholder function triggered");

            try
            {
                var accessToken = AuthHelper.GetAccessToken(req);
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"CreateShareholder request body: {requestBody}");
                
                var shareholder = JsonSerializer.Deserialize<Shareholder>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation($"CreateShareholder deserialized - Name: {shareholder.Name}, Type: {shareholder.ShareholderType}, ShareClassId: {shareholder.ShareClassId}, SharesOwned: {shareholder.SharesOwned}");
                
                await EnsureShareCertificateNumberAsync(shareholder);
                shareholder.ShareClassName = ResolveShareClassName(shareholder.ShareClassId, shareholder.ShareClassName);
                var createdShareholder = await _shareholderRepository.CreateAsync(shareholder);

                try
                {
                    if (string.IsNullOrWhiteSpace(createdShareholder?.Email))
                    {
                        _logger.LogInformation("Shareholder email not provided. Skipping certificate email.");
                    }
                    else
                    {
                        var companySettings = await _companySettingsRepository.GetDefaultAsync();
                        if (companySettings == null)
                        {
                            _logger.LogWarning("Company settings not found. Skipping certificate email.");
                        }
                        else
                        {
                            var shareClass = ResolveShareClass(createdShareholder.ShareClassId, createdShareholder.ShareClassName);
                            var certificateHtml = await GenerateShareCertificateHtmlAsync(createdShareholder, companySettings, shareClass);
                            var pdfBytes = HtmlPdfService.ConvertHtmlToPdf(certificateHtml);

                            var certificateNumber = !string.IsNullOrWhiteSpace(createdShareholder.ShareCertificateNumber)
                                ? createdShareholder.ShareCertificateNumber
                                : $"SC-{DateTime.Now.Year}-{createdShareholder.Id:D3}";

                            var shareClassName = shareClass?.DisplayName ?? shareClass?.Name ?? "shares";
                            var issueDate = createdShareholder.DateOfIssue ?? DateTime.Now;

                            var attachmentName = $"{createdShareholder.Name?.Replace(" ", "_") ?? "shareholder"}-Share-Certificate-{certificateNumber}.pdf";

                            await _blobStorageService.UploadCompanyDocumentAsync(
                                attachmentName,
                                pdfBytes,
                                "Share Certificate",
                                createdShareholder.Name,
                                null,
                                true,
                                certificateNumber,
                                issueDate,
                                null,
                                $"Share certificate for {createdShareholder.SharesOwned} {shareClassName}");

                            var sendResult = await _emailService.SendShareCertificateEmailAsync(
                                createdShareholder.Email,
                                createdShareholder,
                                certificateNumber,
                                shareClassName,
                                issueDate,
                                pdfBytes,
                                accessToken);

                            if (!sendResult.Success)
                            {
                                _logger.LogWarning($"Share certificate email failed for shareholder {createdShareholder.Id}: {sendResult.Error}");
                            }
                        }
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Error sending share certificate email");
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(createdShareholder);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating shareholder: {ex.Message}\nStack: {ex.StackTrace}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UpdateShareholder")]
        public async Task<HttpResponseData> UpdateShareholder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "shareholders/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"UpdateShareholder function triggered for ID: {id}");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var shareholder = JsonSerializer.Deserialize<Shareholder>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                shareholder.Id = id;
                shareholder.ShareClassName = ResolveShareClassName(shareholder.ShareClassId, shareholder.ShareClassName);
                await _shareholderRepository.UpdateAsync(shareholder);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(shareholder);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating shareholder {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DeleteShareholder")]
        public async Task<HttpResponseData> DeleteShareholder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "shareholders/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"DeleteShareholder function triggered for ID: {id}");

            var blocked = await _guard.GuardAsync(req, "shareholder");
            if (blocked != null) return blocked;

            try
            {
                await _shareholderRepository.DeleteAsync(id);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Shareholder deleted successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting shareholder {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetShareClasses")]
        public async Task<HttpResponseData> GetShareClasses(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shareclasses")] HttpRequestData req)
        {
            _logger.LogInformation("GetShareClasses function triggered");

            try
            {
                var shareClasses = GetDefaultShareClasses();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(shareClasses);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting share classes");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetShareholderCertificate")]
        public async Task<HttpResponseData> GetShareholderCertificate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shareholders/{id}/certificate")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"GetShareholderCertificate function triggered for ID: {id}");

            try
            {
                // Get shareholder details
                var shareholder = await _shareholderRepository.GetByIdAsync(id);
                if (shareholder == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Shareholder not found");
                    return notFound;
                }

                // Get company settings
                var companySettings = await _companySettingsRepository.GetDefaultAsync();
                if (companySettings == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Company settings not configured");
                    return badRequest;
                }

                // Get share class if specified
                var shareClass = ResolveShareClass(shareholder.ShareClassId, shareholder.ShareClassName);

                // Generate certificate using HTML template
                var certificateHtml = await GenerateShareCertificateHtmlAsync(shareholder, companySettings, shareClass);

                // Convert HTML to bytes
                var htmlBytes = System.Text.Encoding.UTF8.GetBytes(certificateHtml);

                var fileName = $"{shareholder.Name.Replace(" ", "_")}-Certificate-{shareholder.ShareCertificateNumber}.html";

                // Return HTML for viewing
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");
                await response.WriteStringAsync(certificateHtml);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating share certificate");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("SendShareholderCertificateEmail")]
        public async Task<HttpResponseData> SendShareholderCertificateEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shareholders/{id}/certificate/email")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"SendShareholderCertificateEmail function triggered for ID: {id}");

            try
            {
                var accessToken = AuthHelper.GetAccessToken(req);
                var shareholder = await _shareholderRepository.GetByIdAsync(id);
                if (shareholder == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Shareholder not found");
                    return notFound;
                }

                var companySettings = await _companySettingsRepository.GetDefaultAsync();
                if (companySettings == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Company settings not configured");
                    return badRequest;
                }

                string? requestedEmail = null;
                try
                {
                    var body = await new StreamReader(req.Body).ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var payload = JsonSerializer.Deserialize<EmailRequest>(body, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        requestedEmail = payload?.Email;
                    }
                }
                catch
                {
                    // Ignore body parsing errors
                }

                var toEmail = !string.IsNullOrWhiteSpace(requestedEmail)
                    ? requestedEmail
                    : (shareholder.Email ?? companySettings.CompanyEmail);

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("No recipient email available for share certificate");
                    return errorResponse;
                }

                var shareClass = ResolveShareClass(shareholder.ShareClassId, shareholder.ShareClassName);
                var certificateHtml = await GenerateShareCertificateHtmlAsync(shareholder, companySettings, shareClass);
                var pdfBytes = HtmlPdfService.ConvertHtmlToPdf(certificateHtml);

                var certificateNumber = !string.IsNullOrWhiteSpace(shareholder.ShareCertificateNumber)
                    ? shareholder.ShareCertificateNumber
                    : $"SC-{DateTime.Now.Year}-{shareholder.Id:D3}";

                var shareClassName = shareClass?.DisplayName ?? shareClass?.Name ?? "shares";
                var issueDate = shareholder.DateOfIssue ?? DateTime.Now;

                var emailResult = await _emailService.SendShareCertificateEmailAsync(
                    toEmail,
                    shareholder,
                    certificateNumber,
                    shareClassName,
                    issueDate,
                    pdfBytes,
                    accessToken);

                if (!emailResult.Success)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync(emailResult.Error ?? "Failed to send share certificate email");
                    return errorResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = "Share certificate email sent successfully", toEmail });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending share certificate email");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("RegenerateAllCertificates")]
        public async Task<HttpResponseData> RegenerateAllCertificates(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shareholders/regenerate-certificates")] HttpRequestData req)
        {
            _logger.LogInformation("RegenerateAllCertificates function triggered");

            try
            {
                var shareholders = await _shareholderRepository.GetAllAsync();
                var companySettings = await _companySettingsRepository.GetDefaultAsync();
                var shareClasses = GetDefaultShareClasses();

                int successCount = 0;
                int failCount = 0;
                var errors = new System.Collections.Generic.List<string>();

                foreach (var shareholder in shareholders)
                {
                    try
                    {
                        ShareClass? shareClass = ResolveShareClass(shareholder.ShareClassId, shareholder.ShareClassName);

                        // Generate certificate
                        var certificateHtml = await GenerateShareCertificateHtmlAsync(shareholder, companySettings, shareClass);
                        var htmlBytes = System.Text.Encoding.UTF8.GetBytes(certificateHtml);

                        var fileName = $"{shareholder.Name.Replace(" ", "_")}-Certificate-{shareholder.ShareCertificateNumber}.html";
                        await _blobStorageService.UploadCompanyDocumentAsync(
                            fileName,
                            htmlBytes,
                            "Share Certificate",
                            shareholder.Name,
                            null,
                            true,
                            shareholder.ShareCertificateNumber,
                            shareholder.DateOfIssue,
                            null,
                            $"Regenerated: Share certificate for {shareholder.SharesOwned} {shareClass?.DisplayName ?? shareClass?.Name ?? "shares"}");

                        successCount++;
                        _logger.LogInformation($"Regenerated certificate for {shareholder.Name}");
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        var errorMsg = $"{shareholder.Name}: {ex.Message}";
                        errors.Add(errorMsg);
                        _logger.LogError(ex, $"Failed to regenerate certificate for {shareholder.Name}");
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    message = "Certificate regeneration completed",
                    successCount,
                    failCount,
                    errors
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error regenerating certificates");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private List<ShareClass> GetDefaultShareClasses()
        {
            return new List<ShareClass>
            {
                new ShareClass
                {
                    Id = 1,
                    Name = "A",
                    DisplayName = "A Ordinary Shares",
                    VotingRights = "1 vote per share",
                    DividendPolicyNote = "Standard dividend rights",
                    IsActive = true,
                    Notes = "Default share class"
                },
                new ShareClass
                {
                    Id = 2,
                    Name = "B",
                    DisplayName = "B Ordinary Shares",
                    VotingRights = "1 vote per share",
                    DividendPolicyNote = "Standard dividend rights",
                    IsActive = true,
                    Notes = "Default share class"
                }
            };
        }

        private ShareClass? ResolveShareClass(int? shareClassId, string? shareClassName)
        {
            var shareClasses = GetDefaultShareClasses();
            if (shareClassId.HasValue)
            {
                return shareClasses.FirstOrDefault(sc => sc.Id == shareClassId.Value);
            }

            if (!string.IsNullOrWhiteSpace(shareClassName))
            {
                if (shareClassName.Contains("B", StringComparison.OrdinalIgnoreCase))
                {
                    return shareClasses.First(sc => sc.Name == "B");
                }
                return shareClasses.First(sc => sc.Name == "A");
            }

            return shareClasses.First(sc => sc.Name == "A");
        }

        private string? ResolveShareClassName(int? shareClassId, string? shareClassName)
        {
            if (!string.IsNullOrWhiteSpace(shareClassName))
            {
                return shareClassName;
            }

            var shareClass = ResolveShareClass(shareClassId, shareClassName);
            return shareClass?.Name;
        }

        private async Task EnsureShareCertificateNumberAsync(Shareholder shareholder)
        {
            if (!string.IsNullOrWhiteSpace(shareholder.ShareCertificateNumber))
            {
                return;
            }

            var existingShareholders = await _shareholderRepository.GetAllAsync();
            var maxCertNumber = 0;

            foreach (var sh in existingShareholders)
            {
                if (!string.IsNullOrEmpty(sh.ShareCertificateNumber) && sh.ShareCertificateNumber.StartsWith("SC-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = sh.ShareCertificateNumber.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out var num))
                    {
                        maxCertNumber = Math.Max(maxCertNumber, num);
                    }
                }
            }

            var year = DateTime.UtcNow.Year;
            shareholder.ShareCertificateNumber = $"SC-{year}-{(maxCertNumber + 1):D3}";
        }

        private async Task<string> GenerateShareCertificateHtmlAsync(
            Shareholder shareholder,
            CompanySettings company,
            ShareClass? shareClass)
        {
            var templateType = (shareClass?.Name == "B" || shareClass?.DisplayName?.StartsWith("B", StringComparison.OrdinalIgnoreCase) == true)
                ? "Share Certificate - B Ordinary Shares"
                : "Share Certificate - A Ordinary Shares";

            var templateHtml = await GetTemplateHtmlAsync(templateType);
            var allShareholders = await _shareholderRepository.GetAllAsync();
            var shareNumbersRange = CalculateShareNumbersRange(shareholder, allShareholders.ToList());

            var certificateNumber = shareholder.ShareCertificateNumber ?? $"SC-{DateTime.Now.Year}-{shareholder.Id:D3}";
            var issueDate = shareholder.DateOfIssue ?? DateTime.Now;
            var issueDateFormatted = issueDate.ToString("dd MMM yyyy");

            var logoDataUrl = await _blobStorageService.GetLogoBase64Async(company.Id);
            var html = TemplatePlaceholderService.ApplyCompanyPlaceholders(templateHtml, company, logoDataUrl, issueDate);

            html = html
                .Replace("{{HOLDER_FULL_NAME}}", shareholder.Name ?? "")
                .Replace("{{SHAREHOLDER_NAME}}", shareholder.Name ?? "")
                .Replace("{{CERTIFICATE_NUMBER}}", certificateNumber)
                .Replace("{{YEAR}}", issueDate.Year.ToString())
                .Replace("{{SEQUENCE}}", shareholder.Id.ToString("D3"))
                .Replace("{{SHARE_CLASS}}", shareClass?.DisplayName ?? shareClass?.Name ?? "Ordinary Shares")
                .Replace("{{NUMBER_OF_SHARES}}", shareholder.SharesOwned.ToString("N0"))
                .Replace("{{SHARE_NUMBERS}}", shareNumbersRange)
                .Replace("{{FROM_TO_NUMBERS}}", shareNumbersRange)
                .Replace("{{DATE_OF_ISSUE}}", issueDate.ToString("dd MMMM yyyy"))
                .Replace("{{ISSUE_DATE_DD_MON_YYYY}}", issueDateFormatted)
                .Replace("{{SIGN_DATE}}", issueDate.ToString("dd MMM yyyy"));

            return html;
        }

        private async Task<string> GetTemplateHtmlAsync(string templateType)
        {
            var documents = await _blobStorageService.GetCompanyDocumentsAsync();
            var template = documents.FirstOrDefault(d =>
                string.Equals(d.DocumentType, "Template", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d.RelatedEntity?.Trim(), templateType, StringComparison.OrdinalIgnoreCase) &&
                d.IsActive);

            if (template == null)
            {
                template = documents.FirstOrDefault(d =>
                    string.Equals(d.DocumentType, "Template", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(d.RelatedEntity?.Trim(), templateType, StringComparison.OrdinalIgnoreCase));
            }

            if (template == null)
            {
                template = documents.FirstOrDefault(d =>
                    string.Equals(d.DocumentType, "Template", StringComparison.OrdinalIgnoreCase) &&
                    (d.FileName?.Contains(templateType, StringComparison.OrdinalIgnoreCase) == true));
            }

            if (template == null)
            {
                throw new Exception($"Template not found: {templateType}. Please upload the template to Company Documents.");
            }

            var (content, _, _) = await _blobStorageService.DownloadCompanyDocumentAsync(template.BlobName);
            return Encoding.UTF8.GetString(content);
        }

        private string CalculateShareNumbersRange(Shareholder shareholder, List<Shareholder> allShareholders)
        {
            var issueDate = shareholder.DateOfIssue ?? DateTime.Now;

            var previousShareholdersSameClass = allShareholders
                .Where(s => s.ShareClassId == shareholder.ShareClassId && (s.DateOfIssue ?? issueDate) < issueDate)
                .OrderBy(s => s.DateOfIssue)
                .ToList();

            int startNumber = previousShareholdersSameClass.Sum(s => s.SharesOwned) + 1;
            int endNumber = startNumber + shareholder.SharesOwned - 1;

            if (shareholder.SharesOwned <= 1)
            {
                return startNumber.ToString();
            }

            return $"{startNumber}-{endNumber}";
        }

        private class EmailRequest
        {
            public string? Email { get; set; }
        }
    }
}
