using System;

namespace FinanceHubFunctions.Models
{
    public class Accountant
    {
        public int Id { get; set; }
        public string? ClerkUserId { get; set; }       // Clerk user_xxx ID (set on invite acceptance)
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FirmName { get; set; }
        public string Status { get; set; } = "Invited"; // Invited | Active | Disabled
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
    }
}
