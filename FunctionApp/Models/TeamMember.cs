using System;

namespace FinanceHubFunctions.Models
{
    public class TeamMember
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int? EmployeeId { get; set; }          // FK to Employees (linked after acceptance)
        public string? ClerkUserId { get; set; }       // Clerk user_xxx ID
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Role { get; set; } = "Employee"; // Employee | Approver | Admin
        public string Status { get; set; } = "Invited"; // Invited | Active | Disabled
        public string? InviteToken { get; set; }
        public string? InvitedBy { get; set; }         // Admin's Entra ObjectId
        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? DisabledAt { get; set; }
    }
}
