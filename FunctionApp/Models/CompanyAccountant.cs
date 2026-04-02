using System;

namespace FinanceHubFunctions.Models
{
    public class CompanyAccountant
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }              // FK to CompanySettings.Id
        public int AccountantId { get; set; }           // FK to Accountants.Id
        public string AccessLevel { get; set; } = "ReadOnly";
        public string? InviteToken { get; set; }
        public string? InvitedBy { get; set; }          // Admin's Entra ObjectId
        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public string Status { get; set; } = "Invited"; // Invited | Active | Revoked
    }
}
