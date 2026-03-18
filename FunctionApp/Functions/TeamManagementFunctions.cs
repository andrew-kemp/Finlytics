using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;

namespace FinanceHubFunctions.Functions
{
    /// <summary>
    /// Team management and approval APIs — called from the admin app (MSAL auth).
    /// Manages team members (invite, list, disable) and expense/mileage approvals.
    /// </summary>
    public class TeamManagementFunctions
    {
        private readonly ITeamMemberRepository _teamMemberRepo;
        private readonly IExpenseRepository _expenseRepo;
        private readonly IMileageTripRepository _mileageRepo;
        private readonly IEmployeeRepository _employeeRepo;
        private readonly ICompanySettingsRepository _companySettingsRepo;
        private readonly EmailService? _emailService;

        public TeamManagementFunctions(
            ITeamMemberRepository teamMemberRepo,
            IExpenseRepository expenseRepo,
            IMileageTripRepository mileageRepo,
            IEmployeeRepository employeeRepo,
            ICompanySettingsRepository companySettingsRepo,
            EmailService? emailService = null)
        {
            _teamMemberRepo = teamMemberRepo;
            _expenseRepo = expenseRepo;
            _mileageRepo = mileageRepo;
            _employeeRepo = employeeRepo;
            _companySettingsRepo = companySettingsRepo;
            _emailService = emailService;
        }

        // ─────────────────────────────────────────────────
        // GET /api/team/members — List all team members
        // ─────────────────────────────────────────────────
        [Function("TeamListMembers")]
        public async Task<HttpResponseData> ListMembers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "team/members")] HttpRequestData req)
        {
            var members = await _teamMemberRepo.GetAllAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(members.OrderBy(m => m.DisplayName ?? m.Email));
            return response;
        }

        // ─────────────────────────────────────────────────
        // POST /api/team/invite — Invite an employee
        // ─────────────────────────────────────────────────
        [Function("TeamInviteMember")]
        public async Task<HttpResponseData> InviteMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "team/invite")] HttpRequestData req)
        {
            try
            {
                var body = await req.ReadAsStringAsync();
                var payload = JsonSerializer.Deserialize<InviteRequest>(body ?? "", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(new { error = "Email is required" });
                    return badReq;
                }

                // Get company ID (for now, use 1 — single-tenant)
                var companySettings = await _companySettingsRepo.GetDefaultAsync();
                var companyId = companySettings?.Id ?? 1;

                // Check for existing invite
                var existing = await _teamMemberRepo.GetByEmailAndCompanyAsync(payload.Email, companyId);
                if (existing != null)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = "This email has already been invited", existingStatus = existing.Status });
                    return conflict;
                }

                // Try to link to an existing employee record
                var employees = await _employeeRepo.GetAllAsync();
                var linkedEmployee = employees.FirstOrDefault(e =>
                    string.Equals(e.Email, payload.Email, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.PersonalEmail, payload.Email, StringComparison.OrdinalIgnoreCase));

                // Generate invite token
                var inviteToken = Guid.NewGuid().ToString("N");

                var member = new TeamMember
                {
                    CompanyId = companyId,
                    EmployeeId = linkedEmployee?.Id,
                    Email = payload.Email.Trim().ToLowerInvariant(),
                    DisplayName = payload.DisplayName ?? linkedEmployee?.Name,
                    Role = payload.Role ?? "Employee",
                    Status = "Invited",
                    InviteToken = inviteToken,
                    InvitedBy = payload.InvitedBy,
                    InvitedAt = DateTime.UtcNow
                };

                var created = await _teamMemberRepo.CreateAsync(member);

                // Build invite URL
                var portalDomain = Environment.GetEnvironmentVariable("EMPLOYEE_PORTAL_URL")
                    ?? "https://expensedev.finlytics.co.uk";
                var inviteUrl = $"{portalDomain}/accept-invite?token={inviteToken}";

                // Send invite email if email service is available
                if (_emailService != null)
                {
                    try
                    {
                        var companyName = companySettings?.CompanyName ?? "Finlytics";
                        var emailBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;max-width:600px;margin:0 auto;padding:20px;color:#333;'>
  <div style='text-align:center;margin-bottom:24px;'>
    <h2 style='color:#1a1a2e;margin:0 0 4px;'>You've been invited to {companyName} Expenses</h2>
    <p style='color:#666;margin:0;font-size:14px;'>Submit expenses and mileage claims online</p>
  </div>
  <div style='background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;'>
    <p style='margin:0 0 16px;'>Hi {member.DisplayName ?? "there"},</p>
    <p style='margin:0 0 20px;'>You've been invited to submit expenses and mileage claims via the {companyName} Expense Portal.</p>
    <div style='text-align:center;margin:24px 0;'>
      <a href='{inviteUrl}' style='display:inline-block;padding:14px 32px;background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:white;text-decoration:none;border-radius:8px;font-weight:600;font-size:16px;'>Accept Invitation</a>
    </div>
    <p style='margin:16px 0 0;font-size:13px;color:#666;'>Or copy this link:<br/>
      <a href='{inviteUrl}' style='color:#667eea;word-break:break-all;'>{inviteUrl}</a>
    </p>
  </div>
  <p style='color:#999;font-size:12px;text-align:center;margin:0;'>
    This invitation was sent by {companyName}. If you weren't expecting this, you can safely ignore it.
  </p>
</body>
</html>";

                        var fromAddress = Environment.GetEnvironmentVariable("INVITE_FROM_EMAIL");
                        // If no override set, use the company's default SMTP from address

                        var (success, error) = await _emailService.SendSystemEmailAsync(
                            member.Email,
                            $"You're invited to {companyName} Expenses",
                            emailBody,
                            fromAddressOverride: fromAddress);

                        if (success)
                        {
                            Console.WriteLine($"Invite email sent to {member.Email}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to send invite email to {member.Email}: {error}");
                        }
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"Failed to send invite email: {emailEx.Message}");
                        // Don't fail the invite if email fails
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    member = created,
                    inviteUrl
                });
                return response;
            }
            catch (Exception ex)
            {
                var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResp.WriteAsJsonAsync(new { error = ex.Message });
                return errResp;
            }
        }

        // ─────────────────────────────────────────────────
        // PUT /api/team/members/{id} — Update member role/status
        // ─────────────────────────────────────────────────
        [Function("TeamUpdateMember")]
        public async Task<HttpResponseData> UpdateMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "team/members/{id:int}")] HttpRequestData req,
            int id)
        {
            var member = await _teamMemberRepo.GetByIdAsync(id);
            if (member == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { error = "Member not found" });
                return notFound;
            }

            var body = await req.ReadAsStringAsync();
            var updates = JsonSerializer.Deserialize<Dictionary<string, string>>(body ?? "{}");

            if (updates != null)
            {
                if (updates.ContainsKey("role")) member.Role = updates["role"];
                if (updates.ContainsKey("status"))
                {
                    member.Status = updates["status"];
                    if (updates["status"] == "Disabled")
                        member.DisabledAt = DateTime.UtcNow;
                }
                if (updates.ContainsKey("displayName")) member.DisplayName = updates["displayName"];
            }

            await _teamMemberRepo.UpdateAsync(member);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(member);
            return response;
        }

        // ─────────────────────────────────────────────────
        // DELETE /api/team/members/{id} — Remove a team member
        // ─────────────────────────────────────────────────
        [Function("TeamDeleteMember")]
        public async Task<HttpResponseData> DeleteMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "team/members/{id:int}")] HttpRequestData req,
            int id)
        {
            await _teamMemberRepo.DeleteAsync(id);
            return req.CreateResponse(HttpStatusCode.NoContent);
        }

        // ─────────────────────────────────────────────────
        // GET /api/team/approvals — List pending approvals
        // ─────────────────────────────────────────────────
        [Function("TeamListApprovals")]
        public async Task<HttpResponseData> ListApprovals(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "team/approvals")] HttpRequestData req)
        {
            var expenses = await _expenseRepo.GetAllAsync();
            var mileage = await _mileageRepo.GetAllAsync();
            var members = await _teamMemberRepo.GetAllAsync();
            var memberLookup = members.ToDictionary(m => m.Id, m => m);

            var pendingExpenses = expenses
                .Where(e => e.ApprovalStatus == "Submitted")
                .Select(e => new
                {
                    Type = "expense",
                    e.Id,
                    e.ExpenseId,
                    e.Supplier,
                    e.Category,
                    e.AmountGross,
                    e.EntryDate,
                    e.SubmittedAt,
                    e.Notes,
                    SubmittedBy = e.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(e.SubmittedByTeamMemberId.Value)
                        ? memberLookup[e.SubmittedByTeamMemberId.Value].DisplayName ?? memberLookup[e.SubmittedByTeamMemberId.Value].Email
                        : "Unknown",
                    HasReceipt = e.Attachments != null && e.Attachments.Count > 0
                });

            var pendingMileage = mileage
                .Where(t => t.ApprovalStatus == "Submitted")
                .Select(t => new
                {
                    Type = "mileage",
                    t.Id,
                    ExpenseId = t.TripId,
                    Supplier = (string?)null,
                    Category = t.Category,
                    AmountGross = (decimal?)t.TotalAmount,
                    EntryDate = (DateTime?)t.TripDate,
                    t.SubmittedAt,
                    Notes = $"{t.StartLocation} → {t.EndLocation} ({t.Miles} miles)",
                    SubmittedBy = t.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(t.SubmittedByTeamMemberId.Value)
                        ? memberLookup[t.SubmittedByTeamMemberId.Value].DisplayName ?? memberLookup[t.SubmittedByTeamMemberId.Value].Email
                        : "Unknown",
                    HasReceipt = false
                });

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pendingExpenses.Cast<object>().Concat(pendingMileage.Cast<object>()).OrderByDescending(x => ((dynamic)x).SubmittedAt));
            return response;
        }

        // ─────────────────────────────────────────────────
        // POST /api/team/approvals/{type}/{id}/approve
        // ─────────────────────────────────────────────────
        [Function("TeamApproveItem")]
        public async Task<HttpResponseData> ApproveItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "team/approvals/{type}/{id:int}/approve")] HttpRequestData req,
            string type, int id)
        {
            if (type == "expense")
            {
                var expense = await _expenseRepo.GetByIdAsync(id);
                if (expense == null || expense.ApprovalStatus != "Submitted")
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Expense not found or not pending" });
                    return notFound;
                }

                expense.ApprovalStatus = "Approved";
                expense.ApprovedAt = DateTime.UtcNow;
                await _expenseRepo.UpdateAsync(expense);
            }
            else if (type == "mileage")
            {
                var trip = await _mileageRepo.GetByIdAsync(id);
                if (trip == null || trip.ApprovalStatus != "Submitted")
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Mileage trip not found or not pending" });
                    return notFound;
                }

                trip.ApprovalStatus = "Approved";
                trip.ApprovedAt = DateTime.UtcNow;
                await _mileageRepo.UpdateAsync(trip);
            }
            else
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(new { error = "Type must be 'expense' or 'mileage'" });
                return badReq;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Approved" });
            return response;
        }

        // ─────────────────────────────────────────────────
        // POST /api/team/approvals/{type}/{id}/reject
        // ─────────────────────────────────────────────────
        [Function("TeamRejectItem")]
        public async Task<HttpResponseData> RejectItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "team/approvals/{type}/{id:int}/reject")] HttpRequestData req,
            string type, int id)
        {
            var body = await req.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(body ?? "{}");
            var reason = payload?.GetValueOrDefault("reason") ?? "No reason provided";

            if (type == "expense")
            {
                var expense = await _expenseRepo.GetByIdAsync(id);
                if (expense == null || expense.ApprovalStatus != "Submitted")
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Expense not found or not pending" });
                    return notFound;
                }

                expense.ApprovalStatus = "Rejected";
                expense.RejectionReason = reason;
                await _expenseRepo.UpdateAsync(expense);
            }
            else if (type == "mileage")
            {
                var trip = await _mileageRepo.GetByIdAsync(id);
                if (trip == null || trip.ApprovalStatus != "Submitted")
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Mileage trip not found or not pending" });
                    return notFound;
                }

                trip.ApprovalStatus = "Rejected";
                trip.RejectionReason = reason;
                await _mileageRepo.UpdateAsync(trip);
            }
            else
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(new { error = "Type must be 'expense' or 'mileage'" });
                return badReq;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Rejected", reason });
            return response;
        }

        // ─────────────────────────────────────────────────
        // Request models
        // ─────────────────────────────────────────────────
        private class InviteRequest
        {
            public string? Email { get; set; }
            public string? DisplayName { get; set; }
            public string? Role { get; set; }
            public string? InvitedBy { get; set; }
        }
    }
}
