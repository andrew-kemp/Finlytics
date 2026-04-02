using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
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
            var employees = await _employeeRepo.GetAllAsync();
            var employeeLookup = employees.ToDictionary(e => e.Id, e => e);

            var enriched = members
                .OrderBy(m => m.DisplayName ?? m.Email)
                .Select(m =>
                {
                    Employee emp = null;
                    if (m.EmployeeId.HasValue && employeeLookup.ContainsKey(m.EmployeeId.Value))
                        emp = employeeLookup[m.EmployeeId.Value];
                    return new
                    {
                        m.Id,
                        m.CompanyId,
                        m.EmployeeId,
                        EmployeeNumber = emp?.EmployeeNumber,
                        EmployeeName = emp?.Name,
                        m.ClerkUserId,
                        m.Email,
                        DisplayName = string.IsNullOrWhiteSpace(m.DisplayName) ? emp?.Name : m.DisplayName,
                        m.Role,
                        m.Status,
                        m.InviteToken,
                        m.InvitedBy,
                        m.InvitedAt,
                        m.AcceptedAt,
                        m.DisabledAt
                    };
                })
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(enriched);
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
        // Returns { expenses: [...], mileage: [...] }
        // ─────────────────────────────────────────────────
        [Function("TeamListApprovals")]
        public async Task<HttpResponseData> ListApprovals(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "team/approvals")] HttpRequestData req)
        {
            var expenses = await _expenseRepo.GetAllAsync();
            var mileage = await _mileageRepo.GetAllAsync();
            var members = await _teamMemberRepo.GetAllAsync();
            var employees = await _employeeRepo.GetAllAsync();
            var memberLookup = members.ToDictionary(m => m.Id, m => m);
            var employeeLookup = employees.ToDictionary(e => e.Id, e => e);

            var pendingExpenses = expenses
                .Where(e => e.ApprovalStatus == "Submitted")
                .OrderByDescending(e => e.SubmittedAt)
                .Select(e =>
                {
                    TeamMember tm = null;
                    Employee emp = null;
                    if (e.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(e.SubmittedByTeamMemberId.Value))
                    {
                        tm = memberLookup[e.SubmittedByTeamMemberId.Value];
                        if (tm.EmployeeId.HasValue && employeeLookup.ContainsKey(tm.EmployeeId.Value))
                            emp = employeeLookup[tm.EmployeeId.Value];
                    }
                    return new
                    {
                        e.Id,
                        e.ExpenseId,
                        e.Supplier,
                        e.Category,
                        e.AmountNet,
                        e.VATAmount,
                        e.AmountGross,
                        e.EntryDate,
                        e.SubmittedAt,
                        e.Notes,
                        e.Reference,
                        e.PaymentMethod,
                        EmployeeName = ResolveEmployeeName(tm, emp),
                        EmployeeEmail = tm?.Email,
                        EmployeeBankName = emp?.BankAccountName,
                        EmployeeBankAccount = emp?.BankAccountNumber,
                        EmployeeBankSortCode = emp?.BankSortCode,
                        HasReceipt = e.Attachments != null && e.Attachments.Count > 0
                    };
                })
                .ToList();

            var pendingMileage = mileage
                .Where(t => t.ApprovalStatus == "Submitted")
                .OrderByDescending(t => t.SubmittedAt)
                .Select(t =>
                {
                    TeamMember tm = null;
                    Employee emp = null;
                    if (t.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(t.SubmittedByTeamMemberId.Value))
                    {
                        tm = memberLookup[t.SubmittedByTeamMemberId.Value];
                        if (tm.EmployeeId.HasValue && employeeLookup.ContainsKey(tm.EmployeeId.Value))
                            emp = employeeLookup[tm.EmployeeId.Value];
                    }
                    return new
                    {
                        t.Id,
                        TripId = t.TripId,
                        From = t.StartLocation,
                        To = t.EndLocation,
                        t.Miles,
                        Category = t.Category,
                        Amount = t.TotalAmount,
                        Date = t.TripDate,
                        t.SubmittedAt,
                        Notes = $"{t.StartLocation} → {t.EndLocation} ({t.Miles} miles)",
                        EmployeeName = ResolveEmployeeName(tm, emp),
                        EmployeeEmail = tm?.Email,
                        HasReceipt = false
                    };
                })
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { expenses = pendingExpenses, mileage = pendingMileage });
            return response;
        }

        // ─────────────────────────────────────────────────
        // POST /api/team/approvals/{type}/{id}/approve
        // ─────────────────────────────────────────────────

        // ─────────────────────────────────────────────────
        // GET /api/team/approvals/history — Approved + rejected employee expenses
        // ─────────────────────────────────────────────────
        [Function("TeamListApprovalHistory")]
        public async Task<HttpResponseData> ListApprovalHistory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "team/approvals/history")] HttpRequestData req)
        {
            var expenses = await _expenseRepo.GetAllAsync();
            var mileage = await _mileageRepo.GetAllAsync();
            var members = await _teamMemberRepo.GetAllAsync();
            var employees = await _employeeRepo.GetAllAsync();
            var memberLookup = members.ToDictionary(m => m.Id, m => m);
            var employeeLookup = employees.ToDictionary(e => e.Id, e => e);

            var historyExpenses = expenses
                .Where(e => e.SubmittedByTeamMemberId.HasValue
                    && (e.ApprovalStatus == "Approved" || e.ApprovalStatus == "Rejected"))
                .OrderByDescending(e => e.ApprovedAt ?? e.SubmittedAt)
                .Select(e =>
                {
                    TeamMember tm = null;
                    Employee emp = null;
                    if (e.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(e.SubmittedByTeamMemberId.Value))
                    {
                        tm = memberLookup[e.SubmittedByTeamMemberId.Value];
                        if (tm.EmployeeId.HasValue && employeeLookup.ContainsKey(tm.EmployeeId.Value))
                            emp = employeeLookup[tm.EmployeeId.Value];
                    }
                    return new
                    {
                        e.Id,
                        e.ExpenseId,
                        e.Supplier,
                        e.Category,
                        e.AmountNet,
                        e.VATAmount,
                        e.AmountGross,
                        e.EntryDate,
                        e.SubmittedAt,
                        e.ApprovalStatus,
                        e.ApprovedAt,
                        e.DatePaid,
                        e.PaymentMethod,
                        e.RejectionReason,
                        EmployeeName = ResolveEmployeeName(tm, emp),
                        EmployeeEmail = tm?.Email
                    };
                })
                .ToList();

            var historyMileage = mileage
                .Where(t => t.SubmittedByTeamMemberId.HasValue
                    && (t.ApprovalStatus == "Approved" || t.ApprovalStatus == "Rejected"))
                .OrderByDescending(t => t.ApprovedAt ?? t.SubmittedAt)
                .Select(t =>
                {
                    TeamMember tm = null;
                    Employee emp = null;
                    if (t.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(t.SubmittedByTeamMemberId.Value))
                    {
                        tm = memberLookup[t.SubmittedByTeamMemberId.Value];
                        if (tm.EmployeeId.HasValue && employeeLookup.ContainsKey(tm.EmployeeId.Value))
                            emp = employeeLookup[tm.EmployeeId.Value];
                    }
                    return new
                    {
                        t.Id,
                        TripId = t.TripId,
                        From = t.StartLocation,
                        To = t.EndLocation,
                        t.Miles,
                        Amount = t.TotalAmount,
                        Date = t.TripDate,
                        t.SubmittedAt,
                        t.ApprovalStatus,
                        t.ApprovedAt,
                        t.RejectionReason,
                        EmployeeName = ResolveEmployeeName(tm, emp),
                        EmployeeEmail = tm?.Email
                    };
                })
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { expenses = historyExpenses, mileage = historyMileage });
            return response;
        }

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
                expense.DatePaid = DateTime.UtcNow;
                expense.PaymentMethod = "Employee Claim — Bank Transfer";
                await _expenseRepo.UpdateAsync(expense);

                // Send approval notification email
                await SendApprovalEmailAsync(new[] { expense }, null);
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
        // POST /api/team/approvals/batch — Batch approve expenses
        // Body: { "expenseIds": [1,2,3], "mileageIds": [4,5] }
        // ─────────────────────────────────────────────────
        [Function("TeamBatchApprove")]
        public async Task<HttpResponseData> BatchApprove(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "team/approvals/batch")] HttpRequestData req)
        {
            try
            {
                var body = await req.ReadAsStringAsync();
                var payload = JsonSerializer.Deserialize<BatchApproveRequest>(body ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (payload == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(new { error = "Invalid request body" });
                    return badReq;
                }

                var approvedExpenses = new List<Expense>();
                var approvedMileage = new List<MileageTrip>();
                int approvedCount = 0;

                // Approve expenses
                if (payload.ExpenseIds?.Any() == true)
                {
                    foreach (var eid in payload.ExpenseIds)
                    {
                        var expense = await _expenseRepo.GetByIdAsync(eid);
                        if (expense != null && expense.ApprovalStatus == "Submitted")
                        {
                            expense.ApprovalStatus = "Approved";
                            expense.ApprovedAt = DateTime.UtcNow;
                            expense.DatePaid = DateTime.UtcNow;
                            expense.PaymentMethod = "Employee Claim — Bank Transfer";
                            await _expenseRepo.UpdateAsync(expense);
                            approvedExpenses.Add(expense);
                            approvedCount++;
                        }
                    }
                }

                // Approve mileage
                if (payload.MileageIds?.Any() == true)
                {
                    foreach (var mid in payload.MileageIds)
                    {
                        var trip = await _mileageRepo.GetByIdAsync(mid);
                        if (trip != null && trip.ApprovalStatus == "Submitted")
                        {
                            trip.ApprovalStatus = "Approved";
                            trip.ApprovedAt = DateTime.UtcNow;
                            await _mileageRepo.UpdateAsync(trip);
                            approvedMileage.Add(trip);
                            approvedCount++;
                        }
                    }
                }

                // Send batch approval emails + CSV
                if (approvedExpenses.Any() || approvedMileage.Any())
                {
                    await SendApprovalEmailAsync(approvedExpenses, approvedMileage);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = $"Approved {approvedCount} item(s)", approvedCount });
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

            string employeeEmail = null;
            string employeeName = null;
            string itemDescription = null;
            decimal? amount = null;

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

                itemDescription = $"{expense.Supplier} — {expense.Category}";
                amount = expense.AmountGross;

                // Resolve employee for notification
                if (expense.SubmittedByTeamMemberId.HasValue)
                {
                    var members = await _teamMemberRepo.GetAllAsync();
                    var tm = members.FirstOrDefault(m => m.Id == expense.SubmittedByTeamMemberId.Value);
                    if (tm != null) { employeeEmail = tm.Email; employeeName = tm.DisplayName ?? tm.Email; }
                }
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

                itemDescription = $"{trip.StartLocation} → {trip.EndLocation} ({trip.Miles} miles)";
                amount = trip.TotalAmount;

                if (trip.SubmittedByTeamMemberId.HasValue)
                {
                    var members = await _teamMemberRepo.GetAllAsync();
                    var tm = members.FirstOrDefault(m => m.Id == trip.SubmittedByTeamMemberId.Value);
                    if (tm != null) { employeeEmail = tm.Email; employeeName = tm.DisplayName ?? tm.Email; }
                }
            }
            else
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(new { error = "Type must be 'expense' or 'mileage'" });
                return badReq;
            }

            // Send rejection email to employee
            if (!string.IsNullOrEmpty(employeeEmail) && _emailService != null)
            {
                try
                {
                    var companySettings = await _companySettingsRepo.GetDefaultAsync();
                    var companyName = companySettings?.CompanyName ?? "Finlytics";
                    var fromAddress = Environment.GetEnvironmentVariable("INVITE_FROM_EMAIL");

                    var emailBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;max-width:600px;margin:0 auto;padding:20px;color:#333;'>
  <div style='text-align:center;margin-bottom:24px;'>
    <h2 style='color:#dc2626;margin:0 0 4px;'>❌ Expense Claim Rejected</h2>
  </div>
  <div style='background:#fef2f2;border-radius:8px;padding:24px;border:1px solid #fecaca;'>
    <p>Hi {employeeName},</p>
    <p>Your {type} claim has been rejected:</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
      <tr><td style='padding:8px 0;color:#666;'>Description</td><td style='padding:8px 0;font-weight:600;'>{itemDescription}</td></tr>
      <tr><td style='padding:8px 0;color:#666;'>Amount</td><td style='padding:8px 0;font-weight:600;'>£{amount:F2}</td></tr>
      <tr><td style='padding:8px 0;color:#666;'>Reason</td><td style='padding:8px 0;font-weight:600;color:#dc2626;'>{reason}</td></tr>
    </table>
    <p style='margin:16px 0 0;font-size:0.9em;color:#666;'>You can edit and resubmit this claim from the Expense Portal.</p>
  </div>
  <p style='color:#999;font-size:12px;text-align:center;margin-top:24px;'>
    Sent by {companyName} Expense Management
  </p>
</body>
</html>";

                    await _emailService.SendSystemEmailAsync(employeeEmail, $"Expense Claim Rejected — {itemDescription}", emailBody, fromAddressOverride: fromAddress);
                }
                catch (Exception ex) { Console.WriteLine($"Failed to send rejection email: {ex.Message}"); }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Rejected", reason });
            return response;
        }

        // ─────────────────────────────────────────────────
        // Send approval notification emails + payment CSV
        // ─────────────────────────────────────────────────
        private async Task SendApprovalEmailAsync(IEnumerable<Expense> expenses, IEnumerable<MileageTrip> mileage)
        {
            if (_emailService == null) return;

            try
            {
                var companySettings = await _companySettingsRepo.GetDefaultAsync();
                var companyName = companySettings?.CompanyName ?? "Finlytics";
                var fromAddress = Environment.GetEnvironmentVariable("INVITE_FROM_EMAIL");

                var members = await _teamMemberRepo.GetAllAsync();
                var allEmployees = await _employeeRepo.GetAllAsync();
                var memberLookup = members.ToDictionary(m => m.Id, m => m);
                var employeeLookup = allEmployees.ToDictionary(e => e.Id, e => e);

                // Group expenses by employee
                var expenseList = expenses?.ToList() ?? new List<Expense>();
                var mileageList = mileage?.ToList() ?? new List<MileageTrip>();

                // Determine unique employees to notify
                var employeeTeamMemberIds = new HashSet<int>();
                foreach (var e in expenseList)
                    if (e.SubmittedByTeamMemberId.HasValue)
                        employeeTeamMemberIds.Add(e.SubmittedByTeamMemberId.Value);
                foreach (var t in mileageList)
                    if (t.SubmittedByTeamMemberId.HasValue)
                        employeeTeamMemberIds.Add(t.SubmittedByTeamMemberId.Value);

                // Send individual emails to each employee
                foreach (var tmId in employeeTeamMemberIds)
                {
                    if (!memberLookup.ContainsKey(tmId)) continue;
                    var tm = memberLookup[tmId];
                    var empExpenses = expenseList.Where(e => e.SubmittedByTeamMemberId == tmId).ToList();
                    var empMileage = mileageList.Where(t => t.SubmittedByTeamMemberId == tmId).ToList();

                    var totalAmount = empExpenses.Sum(e => e.AmountGross ?? 0) + empMileage.Sum(t => t.TotalAmount);
                    var itemCount = empExpenses.Count + empMileage.Count;
                    Employee linkedEmp = null;
                    if (tm.EmployeeId.HasValue && employeeLookup.ContainsKey(tm.EmployeeId.Value))
                        linkedEmp = employeeLookup[tm.EmployeeId.Value];
                    var empName = ResolveEmployeeName(tm, linkedEmp);

                    // Build items table
                    var itemRows = new StringBuilder();
                    foreach (var e in empExpenses)
                        itemRows.Append($"<tr><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{e.Supplier}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{e.Category}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{e.EntryDate:dd/MM/yyyy}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;text-align:right;font-weight:600;'>£{e.AmountGross:F2}</td></tr>");
                    foreach (var t in empMileage)
                        itemRows.Append($"<tr><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{t.StartLocation} → {t.EndLocation}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>Mileage ({t.Miles} mi)</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{t.TripDate:dd/MM/yyyy}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;text-align:right;font-weight:600;'>£{t.TotalAmount:F2}</td></tr>");

                    var employeeEmailBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;max-width:600px;margin:0 auto;padding:20px;color:#333;'>
  <div style='text-align:center;margin-bottom:24px;'>
    <h2 style='color:#16a34a;margin:0 0 4px;'>✅ Expense Claim{(itemCount > 1 ? "s" : "")} Approved</h2>
  </div>
  <div style='background:#ecfdf5;border-radius:8px;padding:24px;border:1px solid #86efac;'>
    <p>Hi {empName},</p>
    <p>Great news! {(itemCount > 1 ? $"Your {itemCount} claims have" : "Your claim has")} been approved for a total of <strong>£{totalAmount:F2}</strong>.</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;background:white;border-radius:8px;overflow:hidden;'>
      <thead><tr style='background:#f0fdf4;'>
        <th style='padding:10px 12px;text-align:left;font-size:0.85em;color:#374151;'>Supplier</th>
        <th style='padding:10px 12px;text-align:left;font-size:0.85em;color:#374151;'>Category</th>
        <th style='padding:10px 12px;text-align:left;font-size:0.85em;color:#374151;'>Date</th>
        <th style='padding:10px 12px;text-align:right;font-size:0.85em;color:#374151;'>Amount</th>
      </tr></thead>
      <tbody>{itemRows}</tbody>
      <tfoot><tr style='background:#f0fdf4;'>
        <td colspan='3' style='padding:10px 12px;font-weight:700;'>Total</td>
        <td style='padding:10px 12px;text-align:right;font-weight:700;'>£{totalAmount:F2}</td>
      </tr></tfoot>
    </table>
    <p style='margin:16px 0 0;font-size:0.9em;color:#666;'>Payment will be processed shortly.</p>
  </div>
  <p style='color:#999;font-size:12px;text-align:center;margin-top:24px;'>
    Sent by {companyName} Expense Management
  </p>
</body>
</html>";

                    await _emailService.SendSystemEmailAsync(
                        tm.Email,
                        $"✅ Expense{(itemCount > 1 ? "s" : "")} Approved — £{totalAmount:F2}",
                        employeeEmailBody,
                        fromAddressOverride: fromAddress);
                }

                // Build payment CSV for the approver (admin) — one CSV with all approved items
                var csv = new StringBuilder();
                csv.AppendLine("Employee Name,Employee Email,Bank Account Name,Sort Code,Account Number,Expense ID,Supplier,Category,Date,Net,VAT,Gross,Reference,Payment Method,Notes");
                foreach (var e in expenseList)
                {
                    TeamMember tm = null;
                    Employee emp = null;
                    if (e.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(e.SubmittedByTeamMemberId.Value))
                    {
                        tm = memberLookup[e.SubmittedByTeamMemberId.Value];
                        if (tm.EmployeeId.HasValue && employeeLookup.ContainsKey(tm.EmployeeId.Value))
                            emp = employeeLookup[tm.EmployeeId.Value];
                    }
                    var empName = ResolveEmployeeName(tm, emp);
                    csv.AppendLine(string.Join(",",
                        CsvEscape(empName),
                        CsvEscape(tm?.Email ?? ""),
                        CsvEscape(emp?.BankAccountName ?? ""),
                        CsvEscape(emp?.BankSortCode ?? ""),
                        CsvEscape(emp?.BankAccountNumber ?? ""),
                        CsvEscape(e.ExpenseId ?? ""),
                        CsvEscape(e.Supplier ?? ""),
                        CsvEscape(e.Category ?? ""),
                        e.EntryDate?.ToString("dd/MM/yyyy") ?? "",
                        e.EntryDate?.ToString("dd/MM/yyyy") ?? "",
                        (e.AmountNet ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                        (e.VATAmount ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                        (e.AmountGross ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                        CsvEscape(e.Reference ?? ""),
                        CsvEscape(e.PaymentMethod ?? ""),
                        CsvEscape(e.Notes ?? "")
                    ));
                }
                foreach (var t in mileageList)
                {
                    TeamMember tm = null;
                    if (t.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(t.SubmittedByTeamMemberId.Value))
                        tm = memberLookup[t.SubmittedByTeamMemberId.Value];
                    Employee emp = null;
                    if (tm?.EmployeeId.HasValue == true && employeeLookup.ContainsKey(tm.EmployeeId.Value))
                        emp = employeeLookup[tm.EmployeeId.Value];
                    var empName = ResolveEmployeeName(tm, emp);
                    csv.AppendLine(string.Join(",",
                        CsvEscape(empName),
                        CsvEscape(tm?.Email ?? ""),
                        CsvEscape(emp?.BankAccountName ?? ""),
                        CsvEscape(emp?.BankSortCode ?? ""),
                        CsvEscape(emp?.BankAccountNumber ?? ""),
                        CsvEscape(t.TripId ?? ""),
                        CsvEscape($"{t.StartLocation} → {t.EndLocation}"),
                        CsvEscape($"Mileage ({t.Miles} miles)"),
                        t.TripDate.ToString("dd/MM/yyyy"),
                        t.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                        "0.00",
                        t.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                        "",
                        "Bank Transfer",
                        CsvEscape(t.Purpose ?? "")
                    ));
                }

                // Send CSV email to the admin/approver
                var adminEmail = companySettings?.SmtpFromAddress ?? fromAddress;
                if (!string.IsNullOrEmpty(adminEmail))
                {
                    var totalAll = expenseList.Sum(e => e.AmountGross ?? 0) + mileageList.Sum(t => t.TotalAmount);
                    var totalItems = expenseList.Count + mileageList.Count;

                    // Build admin summary email
                    var adminRows = new StringBuilder();
                    foreach (var e in expenseList)
                    {
                        var tmName = "Unknown";
                        if (e.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(e.SubmittedByTeamMemberId.Value))
                        {
                            var tmRef = memberLookup[e.SubmittedByTeamMemberId.Value];
                            Employee empRef = null;
                            if (tmRef.EmployeeId.HasValue && employeeLookup.ContainsKey(tmRef.EmployeeId.Value))
                                empRef = employeeLookup[tmRef.EmployeeId.Value];
                            tmName = ResolveEmployeeName(tmRef, empRef);
                        }
                        adminRows.Append($"<tr><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{tmName}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{e.Supplier}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{e.Category}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;text-align:right;font-weight:600;'>£{e.AmountGross:F2}</td></tr>");
                    }
                    foreach (var t in mileageList)
                    {
                        var tmName = "Unknown";
                        if (t.SubmittedByTeamMemberId.HasValue && memberLookup.ContainsKey(t.SubmittedByTeamMemberId.Value))
                        {
                            var tmRef = memberLookup[t.SubmittedByTeamMemberId.Value];
                            Employee empRef = null;
                            if (tmRef.EmployeeId.HasValue && employeeLookup.ContainsKey(tmRef.EmployeeId.Value))
                                empRef = employeeLookup[tmRef.EmployeeId.Value];
                            tmName = ResolveEmployeeName(tmRef, empRef);
                        }
                        adminRows.Append($"<tr><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{tmName}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>{t.StartLocation} → {t.EndLocation}</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;'>Mileage ({t.Miles} mi)</td><td style='padding:8px 12px;border-bottom:1px solid #e5e7eb;text-align:right;font-weight:600;'>£{t.TotalAmount:F2}</td></tr>");
                    }

                    var adminEmailBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;max-width:700px;margin:0 auto;padding:20px;color:#333;'>
  <div style='text-align:center;margin-bottom:24px;'>
    <h2 style='color:#1e40af;margin:0 0 4px;'>💰 Expense Approval Summary</h2>
    <p style='color:#666;margin:0;'>{totalItems} item{(totalItems > 1 ? "s" : "")} approved — total £{totalAll:F2}</p>
  </div>
  <div style='background:#eff6ff;border-radius:8px;padding:24px;border:1px solid #bfdbfe;'>
    <table style='width:100%;border-collapse:collapse;background:white;border-radius:8px;overflow:hidden;'>
      <thead><tr style='background:#1e40af;color:white;'>
        <th style='padding:10px 12px;text-align:left;font-size:0.85em;'>Employee</th>
        <th style='padding:10px 12px;text-align:left;font-size:0.85em;'>Supplier</th>
        <th style='padding:10px 12px;text-align:left;font-size:0.85em;'>Category</th>
        <th style='padding:10px 12px;text-align:right;font-size:0.85em;'>Amount</th>
      </tr></thead>
      <tbody>{adminRows}</tbody>
      <tfoot><tr style='background:#eff6ff;'>
        <td colspan='3' style='padding:10px 12px;font-weight:700;'>Total Payment</td>
        <td style='padding:10px 12px;text-align:right;font-weight:700;font-size:1.1em;'>£{totalAll:F2}</td>
      </tr></tfoot>
    </table>
    <p style='margin:16px 0 0;font-size:0.9em;color:#666;'>📎 A payment CSV is attached for banking. Employees have been notified.</p>
  </div>
  <p style='color:#999;font-size:12px;text-align:center;margin-top:24px;'>
    {companyName} Expense Management — {DateTime.UtcNow:dd MMMM yyyy}
  </p>
</body>
</html>";

                    var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
                    var csvFileName = $"expense-payments-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

                    // Use the existing SendEmailWithAttachmentAsync or SendSystemEmailAsync
                    // We need to send with attachment — use the lower-level method
                    await _emailService.SendSystemEmailAsync(
                        adminEmail,
                        $"💰 Expense Approval: {totalItems} item{(totalItems > 1 ? "s" : "")} — £{totalAll:F2}",
                        adminEmailBody,
                        fromAddressOverride: fromAddress,
                        attachmentBytes: csvBytes,
                        attachmentFileName: csvFileName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send approval emails: {ex.Message}");
                // Don't rethrow — approval was already saved
            }
        }
        /// <summary>
        /// Resolves the best display name for a team member: DisplayName → Employee.Name → Email → "Unknown"
        /// </summary>
        private static string ResolveEmployeeName(TeamMember tm, Employee emp)
        {
            if (!string.IsNullOrWhiteSpace(tm?.DisplayName)) return tm.DisplayName;
            if (!string.IsNullOrWhiteSpace(emp?.Name)) return emp.Name;
            if (!string.IsNullOrWhiteSpace(tm?.Email)) return tm.Email;
            return "Unknown";
        }
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
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

        private class BatchApproveRequest
        {
            public List<int> ExpenseIds { get; set; } = new();
            public List<int> MileageIds { get; set; } = new();
        }
    }
}
