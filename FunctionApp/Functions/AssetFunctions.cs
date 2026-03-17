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
    public class AssetFunctions
    {
        private readonly ILogger<AssetFunctions> _logger;
        private readonly IAssetRepository? _assetRepository;
        private readonly BlobStorageService? _blobStorageService;
        private readonly DeletionGuardService? _guard;

        public AssetFunctions(
            ILogger<AssetFunctions> logger,
            IAssetRepository? assetRepository = null,
            BlobStorageService? blobStorageService = null,
            DeletionGuardService? guard = null)
        {
            _logger = logger;
            _assetRepository = assetRepository;
            _blobStorageService = blobStorageService;
            _guard = guard;
        }

        [Function("GetAssets")]
        public async Task<HttpResponseData> GetAssets(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "assets")] HttpRequestData req)
        {
            if (_assetRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Asset repository not available" });
                return response;
            }

            var assets = await _assetRepository.GetAllAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(assets);
            return ok;
        }

        [Function("GetAssetById")]
        public async Task<HttpResponseData> GetAssetById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "assets/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_assetRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Asset repository not available" });
                return response;
            }

            var asset = await _assetRepository.GetByIdAsync(id);
            if (asset == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(asset);
            return ok;
        }

        [Function("GetNextAssetId")]
        public async Task<HttpResponseData> GetNextAssetId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "assets/next-id")] HttpRequestData req)
        {
            if (_assetRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Asset repository not available" });
                return response;
            }

            var nextId = await _assetRepository.GenerateNextAssetIdAsync();
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(new { nextId });
            return ok;
        }

        [Function("CreateAsset")]
        public async Task<HttpResponseData> CreateAsset(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "assets")] HttpRequestData req)
        {
            if (_assetRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Asset repository not available" });
                return response;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var asset = JsonSerializer.Deserialize<Asset>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (asset == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid asset payload" });
                return bad;
            }

            if (string.IsNullOrWhiteSpace(asset.AssetId))
            {
                asset.AssetId = await _assetRepository.GenerateNextAssetIdAsync();
            }

            asset.CreatedDate = DateTime.UtcNow;
            asset.ModifiedDate = DateTime.UtcNow;

            var created = await _assetRepository.CreateAsync(asset);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(created);
            return ok;
        }

        [Function("UpdateAsset")]
        public async Task<HttpResponseData> UpdateAsset(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "assets/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_assetRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Asset repository not available" });
                return response;
            }

            var existing = await _assetRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var asset = JsonSerializer.Deserialize<Asset>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (asset == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Invalid asset payload" });
                return bad;
            }

            asset.Id = id;
            asset.AssetId = existing.AssetId;
            asset.CreatedDate = existing.CreatedDate;
            asset.ModifiedDate = DateTime.UtcNow;

            var updated = await _assetRepository.UpdateAsync(asset);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(updated);
            return ok;
        }

        [Function("DeleteAsset")]
        public async Task<HttpResponseData> DeleteAsset(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "assets/{id:int}")] HttpRequestData req,
            int id)
        {
            if (_assetRepository == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "Asset repository not available" });
                return response;
            }

            if (_guard != null)
            {
                var blocked = await _guard.GuardAsync(req, "asset");
                if (blocked != null) return blocked;
            }

            await _assetRepository.DeleteAsync(id);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("UploadAssetInvoice")]
        public async Task<HttpResponseData> UploadAssetInvoice(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "assets/{id:int}/invoice")] HttpRequestData req,
            int id)
        {
            try
            {
                if (_assetRepository == null || _blobStorageService == null)
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteAsJsonAsync(new { error = "Repository or blob storage not available" });
                    return err;
                }

                var asset = await _assetRepository.GetByIdAsync(id);
                if (asset == null)
                    return req.CreateResponse(HttpStatusCode.NotFound);

                var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
                if (string.IsNullOrEmpty(contentType) || !contentType.Contains("boundary="))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "Invalid Content-Type — no multipart boundary" });
                    return bad;
                }

                var boundary = contentType.Split("boundary=")[1].Trim();
                using var ms = new MemoryStream();
                await req.Body.CopyToAsync(ms);
                var body = ms.ToArray();

                var fileContent = ExtractFileFromMultipart(body, boundary, out string fileName);
                if (fileContent == null || string.IsNullOrEmpty(fileName))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "No file found in upload" });
                    return bad;
                }

                var blobUrl = await _blobStorageService.UploadAssetInvoiceAsync(id, asset.AssetId ?? $"AST-{id}", fileContent, fileName);

                // Persist the URL on the asset record
                asset.InvoiceUrl = blobUrl;
                asset.ModifiedDate = DateTime.UtcNow;
                await _assetRepository.UpdateAsync(asset);

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteAsJsonAsync(new { success = true, fileName, invoiceUrl = blobUrl });
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading invoice for asset {id}");
                var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResp.WriteAsJsonAsync(new { error = ex.Message });
                return errResp;
            }
        }

        private byte[] ExtractFileFromMultipart(byte[] body, string boundary, out string fileName)
        {
            fileName = "";
            try
            {
                var doubleNewline = new byte[] { 13, 10, 13, 10 };
                var boundaryBytes = System.Text.Encoding.UTF8.GetBytes("--" + boundary);
                int searchPos = 0;
                while (searchPos < body.Length)
                {
                    var headerStart = IndexOfBytes(body, System.Text.Encoding.UTF8.GetBytes("Content-Disposition: form-data"), searchPos);
                    if (headerStart == -1) break;
                    var filenameStart = IndexOfBytes(body, System.Text.Encoding.UTF8.GetBytes("filename=\""), headerStart);
                    if (filenameStart == -1) { searchPos = headerStart + 1; continue; }
                    filenameStart += 10;
                    var filenameEnd = Array.IndexOf(body, (byte)'"', filenameStart);
                    if (filenameEnd > filenameStart)
                        fileName = System.Text.Encoding.UTF8.GetString(body, filenameStart, filenameEnd - filenameStart);
                    var headerEnd = IndexOfBytes(body, doubleNewline, headerStart);
                    if (headerEnd == -1) break;
                    var contentStart = headerEnd + 4;
                    var contentEnd = IndexOfBytes(body, boundaryBytes, contentStart);
                    if (contentEnd == -1) contentEnd = body.Length;
                    if (contentEnd >= 2 && body[contentEnd - 2] == 13 && body[contentEnd - 1] == 10) contentEnd -= 2;
                    var fileBytes = new byte[contentEnd - contentStart];
                    Array.Copy(body, contentStart, fileBytes, 0, fileBytes.Length);
                    return fileBytes;
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error extracting file from multipart"); }
            return null;
        }

        private int IndexOfBytes(byte[] array, byte[] pattern, int startIndex)
        {
            for (int i = startIndex; i <= array.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                    if (array[i + j] != pattern[j]) { found = false; break; }
                if (found) return i;
            }
            return -1;
        }
    }
}
