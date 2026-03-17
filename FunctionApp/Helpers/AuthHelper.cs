using Microsoft.Azure.Functions.Worker.Http;
using System.Linq;

namespace FinanceHubFunctions.Helpers
{
    public static class AuthHelper
    {
        public static string? GetAccessToken(HttpRequestData req)
        {
            // Try Authorization header first (standard Bearer token)
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    return authHeader.Substring(7);
                }
            }

            // Fallback to X-SharePoint-Token for backward compatibility
            if (req.Headers.TryGetValues("X-SharePoint-Token", out var tokenHeaders))
            {
                var token = tokenHeaders.FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }

            // For database-only operations, token is optional
            return null;
        }
    }
}
