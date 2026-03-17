using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Data;

namespace FinanceHubFunctions.Functions
{
    public class CompanyDocumentFunctions
    {
        private readonly ILogger _logger;
        private readonly BlobStorageService _blobStorageService;
        private readonly ICompanySettingsRepository _companySettingsRepository;
        private readonly DeletionGuardService _guard;

        public CompanyDocumentFunctions(ILoggerFactory loggerFactory, BlobStorageService blobStorageService, ICompanySettingsRepository companySettingsRepository, DeletionGuardService guard)
        {
            _logger = loggerFactory.CreateLogger<CompanyDocumentFunctions>();
            _blobStorageService = blobStorageService;
            _companySettingsRepository = companySettingsRepository;
            _guard = guard;
        }

        [Function("GetCompanyDocuments")]
        public async Task<HttpResponseData> GetCompanyDocuments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "companydocuments")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("GetCompanyDocuments: Fetching documents from blob storage");
                
                // Get query parameters for filtering
                var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
                var documentType = queryParams.TryGetValue("documentType", out var docTypeValues) ? docTypeValues.FirstOrDefault() : null;
                var customerName = queryParams.TryGetValue("customerName", out var custNameValues) ? custNameValues.FirstOrDefault() : null;
                var customerCode = queryParams.TryGetValue("customerCode", out var custCodeValues) ? custCodeValues.FirstOrDefault() : null;
                var relatedEntity = queryParams.TryGetValue("relatedEntity", out var relatedValues) ? relatedValues.FirstOrDefault() : null;
                
                var documents = await _blobStorageService.GetCompanyDocumentsAsync();
                
                // Apply filters if provided
                if (!string.IsNullOrEmpty(documentType))
                {
                    documents = documents.Where(d => d.DocumentType.Equals(documentType, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogInformation($"Filtered by documentType: {documentType}, count: {documents.Count}");
                }
                
                if (!string.IsNullOrEmpty(customerName))
                {
                    documents = documents.Where(d => d.CustomerName != null && 
                        d.CustomerName.Contains(customerName, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogInformation($"Filtered by customerName: {customerName}, count: {documents.Count}");
                }
                
                if (!string.IsNullOrEmpty(customerCode))
                {
                    documents = documents.Where(d => d.CustomerCode != null && 
                        d.CustomerCode.Equals(customerCode, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogInformation($"Filtered by customerCode: {customerCode}, count: {documents.Count}");
                }

                if (!string.IsNullOrEmpty(relatedEntity))
                {
                    documents = documents.Where(d => d.RelatedEntity != null &&
                        d.RelatedEntity.Equals(relatedEntity, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogInformation($"Filtered by relatedEntity: {relatedEntity}, count: {documents.Count}");
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(documents);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting company documents: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UploadCompanyDocument")]
        public async Task<HttpResponseData> UploadCompanyDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "companydocuments/upload")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("UploadCompanyDocument: Processing upload");
                _logger.LogInformation($"Content-Type: {req.Headers.GetValues("Content-Type").FirstOrDefault()}");
                
                // Parse multipart/form-data
                var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
                if (string.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data"))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Request must be multipart/form-data");
                    return badRequest;
                }

                // Extract boundary from content type
                var boundaryIndex = contentType.IndexOf("boundary=");
                if (boundaryIndex == -1)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("No boundary found in Content-Type header");
                    return badRequest;
                }
                var boundary = contentType.Substring(boundaryIndex + "boundary=".Length).Trim();
                var reader = new MultipartReader(boundary, req.Body);
                
                byte[] fileContent = null;
                string fileName = null;
                string documentType = null;
                string personName = null;
                string personTitle = null;
                bool isActive = false;
                string relatedEntity = null;
                DateTime? documentDate = null;
                DateTime? expiryDate = null;
                string notes = null;

                MultipartSection section;
                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    var hasContentDisposition = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);
                    
                    if (hasContentDisposition)
                    {
                        if (contentDisposition.DispositionType.Equals("form-data"))
                        {
                            var fieldName = contentDisposition.Name.Value?.Trim('"');
                            
                            if (!string.IsNullOrEmpty(contentDisposition.FileName.Value))
                            {
                                // This is a file
                                fileName = contentDisposition.FileName.Value?.Trim('"');
                                using (var ms = new MemoryStream())
                                {
                                    await section.Body.CopyToAsync(ms);
                                    fileContent = ms.ToArray();
                                }
                                _logger.LogInformation($"Received file: {fileName} ({fileContent.Length} bytes)");
                            }
                            else
                            {
                                // This is a regular form field
                                using (var streamReader = new StreamReader(section.Body))
                                {
                                    var value = await streamReader.ReadToEndAsync();
                                    
                                    switch (fieldName.ToLower())
                                    {
                                        case "documenttype":
                                            documentType = value;
                                            break;
                                        case "personname":
                                            personName = value;
                                            break;
                                        case "persontitle":
                                            personTitle = value;
                                            break;
                                        case "isactive":
                                            isActive = bool.TryParse(value, out var active) && active;
                                            break;
                                        case "relatedentity":
                                            relatedEntity = value;
                                            break;
                                        case "documentdate":
                                            if (DateTime.TryParse(value, out var docDate))
                                                documentDate = docDate;
                                            break;
                                        case "expirydate":
                                            if (DateTime.TryParse(value, out var expDate))
                                                expiryDate = expDate;
                                            break;
                                        case "notes":
                                            notes = value;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }

                if (fileContent == null || string.IsNullOrEmpty(fileName))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("No file provided");
                    return badRequest;
                }

                if (string.IsNullOrEmpty(documentType))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("documentType is required");
                    return badRequest;
                }

                _logger.LogInformation($"Uploading: {fileName}, Type: {documentType}, Person: {personName}, Active: {isActive}");

                // Upload to blob storage with metadata
                var blobUrl = await _blobStorageService.UploadCompanyDocumentAsync(
                    fileName, 
                    fileContent, 
                    documentType,
                    personName,
                    personTitle,
                    isActive,
                    relatedEntity,
                    documentDate,
                    expiryDate,
                    notes
                );
                
                // If this is a Company Logo marked as active, update company settings
                if (documentType == "Company Logo" && isActive)
                {
                    _logger.LogInformation($"Updating company settings with new logo URL: {blobUrl}");
                    var companySettings = await _companySettingsRepository.GetDefaultAsync();
                    if (companySettings != null)
                    {
                        companySettings.LogoUrl = blobUrl;
                        await _companySettingsRepository.UpdateAsync(companySettings);
                        _logger.LogInformation("Company logo URL updated successfully");
                    }
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { url = blobUrl, fileName, documentType });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading document: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error uploading document: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DeleteCompanyDocument")]
        public async Task<HttpResponseData> DeleteCompanyDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "companydocuments")] HttpRequestData req)
        {
            try
            {
                var blocked = await _guard.GuardAsync(req, "company document");
                if (blocked != null) return blocked;

                // Get blobName from query string
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var blobName = query["blobName"];
                
                if (string.IsNullOrEmpty(blobName))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("blobName query parameter is required");
                    return errorResponse;
                }
                
                _logger.LogInformation($"DeleteCompanyDocument: Deleting {blobName}");
                
                await _blobStorageService.DeleteCompanyDocumentAsync(blobName);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Document deleted successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UpdateCompanyDocument")]
        public async Task<HttpResponseData> UpdateCompanyDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "companydocuments/update")] HttpRequestData req)
        {
            try
            {
                // Get blobName from query string
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var blobName = query["blobName"];
                
                if (string.IsNullOrEmpty(blobName))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("blobName query parameter is required");
                    return errorResponse;
                }

                // Parse request body
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

                _logger.LogInformation($"UpdateCompanyDocument: Updating {blobName}");
                
                // Extract metadata values
                var documentType = metadata.ContainsKey("documentType") ? metadata["documentType"]?.ToString() : null;
                var personName = metadata.ContainsKey("personName") ? metadata["personName"]?.ToString() : null;
                var personTitle = metadata.ContainsKey("personTitle") ? metadata["personTitle"]?.ToString() : null;
                var isActive = metadata.ContainsKey("isActive") && metadata["isActive"] != null ? Convert.ToBoolean(metadata["isActive"]) : (bool?)null;
                var relatedEntity = metadata.ContainsKey("relatedEntity") ? metadata["relatedEntity"]?.ToString() : null;
                var documentDate = metadata.ContainsKey("documentDate") && metadata["documentDate"] != null ? DateTime.Parse(metadata["documentDate"]?.ToString()!) : (DateTime?)null;
                var expiryDate = metadata.ContainsKey("expiryDate") && metadata["expiryDate"] != null ? DateTime.Parse(metadata["expiryDate"]?.ToString()!) : (DateTime?)null;
                var notes = metadata.ContainsKey("notes") ? metadata["notes"]?.ToString() : null;

                await _blobStorageService.UpdateCompanyDocumentMetadataAsync(
                    blobName,
                    documentType,
                    personName,
                    personTitle,
                    isActive,
                    relatedEntity,
                    documentDate,
                    expiryDate,
                    notes
                );
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Document updated successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating document: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DownloadCompanyDocument")]
        public async Task<HttpResponseData> DownloadCompanyDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "companydocuments/download")] HttpRequestData req)
        {
            try
            {
                // Get blobName from query string
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var blobName = query["blobName"];
                
                if (string.IsNullOrEmpty(blobName))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("blobName query parameter is required");
                    return errorResponse;
                }
                
                _logger.LogInformation($"DownloadCompanyDocument: Downloading {blobName}");
                
                var (fileContent, contentType, fileName) = await _blobStorageService.DownloadCompanyDocumentAsync(blobName);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", contentType);
                response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");
                await response.Body.WriteAsync(fileContent, 0, fileContent.Length);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading document: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("ViewCompanyDocumentPdf")]
        public async Task<HttpResponseData> ViewCompanyDocumentPdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "companydocuments/view-pdf")] HttpRequestData req)
        {
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var blobName = query["blobName"];

                if (string.IsNullOrEmpty(blobName))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("blobName query parameter is required");
                    return errorResponse;
                }

                _logger.LogInformation($"ViewCompanyDocumentPdf: Rendering {blobName}");

                var (fileContent, contentType, fileName) = await _blobStorageService.DownloadCompanyDocumentAsync(blobName);
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

                if (contentType == null || (!contentType.Contains("html") && extension != ".html" && extension != ".htm"))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Only HTML templates can be rendered to PDF.");
                    return badRequest;
                }

                var html = Encoding.UTF8.GetString(fileContent);
                var companySettings = await _companySettingsRepository.GetDefaultAsync();
                if (companySettings != null)
                {
                    var logoDataUrl = await _blobStorageService.GetLogoBase64Async(companySettings.Id);
                    html = TemplatePlaceholderService.ApplyCompanyPlaceholders(html, companySettings, logoDataUrl);
                }

                var pdfBytes = HtmlPdfService.ConvertHtmlToPdf(html);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                var pdfName = Path.ChangeExtension(fileName, ".pdf");
                response.Headers.Add("Content-Disposition", $"inline; filename=\"{pdfName}\"");
                await response.Body.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rendering PDF: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
