using System.Net;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace FinanceHubFunctions.Services
{
    /// <summary>
    /// Guards all delete operations against the AllowDataDeletion master switch in CompanySettings.
    /// When AllowDataDeletion is false (default), all record deletion is blocked — intended for production safety.
    /// </summary>
    public class DeletionGuardService
    {
        private readonly FinanceHubDbContext _db;

        public DeletionGuardService(FinanceHubDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Returns null if deletion is allowed; otherwise returns a 403 Forbidden response.
        /// </summary>
        public async Task<HttpResponseData?> GuardAsync(HttpRequestData req, string entityName)
        {
            var settings = await _db.CompanySettings.FirstOrDefaultAsync();
            if (settings?.AllowDataDeletion == true)
                return null; // deletion is permitted

            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteAsJsonAsync(new
            {
                error = $"Deletion is disabled. Enable 'Allow Data Deletion' in Settings → Company → Compliance & Audit to delete {entityName} records."
            });
            return forbidden;
        }
    }
}
