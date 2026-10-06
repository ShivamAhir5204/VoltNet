using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "Admin,SuperAdmin")]
[Route("admin/subscription")]
public class AdminSubscriptionController : Controller
{
    private readonly AppDbContext _context;

    public AdminSubscriptionController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var plans = await _context.SubscriptionPlans
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
            
        // Calculate active subscribers per plan
        var activeSubs = await _context.OwnerSubscriptions
            .Where(s => s.Status == "Active" && s.EndDate >= DateTime.UtcNow)
            .GroupBy(s => s.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.PlanId, v => v.Count);
            
        ViewBag.ActiveSubs = activeSubs;
            
        return View("~/Views/admin/Subscription/Plans.cshtml", plans);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View("~/Views/admin/Subscription/CreatePlan.cshtml");
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubscriptionPlan model)
    {
        if (ModelState.IsValid)
        {
            model.Id = Guid.NewGuid();
            model.CreatedAt = DateTime.UtcNow;
            
            _context.SubscriptionPlans.Add(model);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Subscription plan created successfully.";
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/admin/Subscription/CreatePlan.cshtml", model);
    }
    
    [HttpPost("toggle-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan != null)
        {
            plan.IsActive = !plan.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Plan '{plan.Name}' is now {(plan.IsActive ? "Active" : "Inactive")}.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();
        return View("~/Views/admin/Subscription/EditPlan.cshtml", plan);
    }

    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SubscriptionPlan model)
    {
        if (id != model.Id) return BadRequest();

        var existingPlan = await _context.SubscriptionPlans.FindAsync(id);
        if (existingPlan == null) return NotFound();

        if (ModelState.IsValid)
        {
            existingPlan.Name = model.Name;
            existingPlan.Description = model.Description;
            existingPlan.PricePerMonth = model.PricePerMonth;
            existingPlan.DurationDays = model.DurationDays;
            existingPlan.MaxStations = model.MaxStations;
            existingPlan.MaxManagersPerStation = model.MaxManagersPerStation;
            existingPlan.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Subscription plan updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/admin/Subscription/EditPlan.cshtml", model);
    }

    [HttpPost("delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();

        var hasSubscriptions = await _context.OwnerSubscriptions.AnyAsync(s => s.PlanId == id);
        if (hasSubscriptions)
        {
            TempData["Error"] = "Cannot delete plan because it has active or past subscribers. Please deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _context.SubscriptionPlans.Remove(plan);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Subscription plan '{plan.Name}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Cancellation Requests Management (Admin) ────────────────────
    [HttpGet("cancellation-requests")]
    public async Task<IActionResult> CancellationRequests()
    {
        var requests = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Owner)
                .ThenInclude(o => o!.User)
            .Include(s => s.Stations)
            .Where(s => s.Status == "CancellationPending" || s.CancellationRequestedAt != null)
            .OrderByDescending(s => s.CancellationRequestedAt)
            .ToListAsync();

        return View("~/Views/admin/Subscription/CancellationRequests.cshtml", requests);
    }

    [HttpPost("approve-cancellation/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCancellation(Guid id, string? adminRemarks)
    {
        var subscription = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Owner)
                .ThenInclude(o => o!.User)
            .Include(s => s.Stations)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subscription == null) return NotFound();

        if (subscription.Status != "CancellationPending")
        {
            TempData["Error"] = "This request is not in a pending state.";
            return RedirectToAction(nameof(CancellationRequests));
        }

        // 1. Unlink all stations and set their status to Approved (offline)
        var ownerUserId = subscription.Owner?.UserId;
        var stations = await _context.Stations
            .Where(s => s.OwnerSubscriptionId == subscription.Id)
            .ToListAsync();

        foreach (var station in stations)
        {
            station.OwnerSubscriptionId = null;
            if (station.Status == "Active")
                station.Status = "Approved"; // Take offline
        }

        // 2. Update subscription status
        subscription.Status = "Cancelled";
        subscription.CancellationProcessedAt = DateTime.UtcNow;
        subscription.CancellationAdminRemarks = string.IsNullOrWhiteSpace(adminRemarks) ? "Cancellation approved by admin." : adminRemarks.Trim();
        subscription.RefundAmount = subscription.AmountPaid; // 100% refund for valid <= 24h cancellations

        await _context.SaveChangesAsync();

        // 3. Send Email Notification to Owner
        var ownerEmail = subscription.Owner?.User?.Email;
        var ownerName = subscription.Owner?.FullName ?? subscription.Owner?.User?.Fullname ?? "Station Owner";
        
        if (!string.IsNullOrWhiteSpace(ownerEmail))
        {
            var emailSubject = $"VoltNet: Subscription Cancellation Approved - {subscription.Plan?.Name} Plan";
            var emailBody = $@"Dear {ownerName},

Your cancellation request for subscription '{subscription.Plan?.Name}' (Payment ID: {subscription.PaymentId}) has been APPROVED by our administration team.

CANCELLATION & REFUND DETAILS:
---------------------------------------------
Plan Name         : {subscription.Plan?.Name}
Purchase Date     : {subscription.CreatedAt:dd MMM yyyy, hh:mm tt} UTC
Cancellation Date : {subscription.CancellationProcessedAt:dd MMM yyyy, hh:mm tt} UTC
Reason Provided   : {subscription.CancellationReason}
Admin Remarks     : {subscription.CancellationAdminRemarks}
Refund Amount     : ₹{subscription.RefundAmount:N2}
Refund Status     : Processed to original payment method (3-5 business days)
Stations Offline  : {stations.Count} station(s) taken offline

If you have any questions, please contact our support team at support@voltnet.com.

Best regards,
VoltNet Admin Team";

            await SendEmailNotificationAsync(ownerEmail, emailSubject, emailBody);
        }

        TempData["Success"] = $"Cancellation approved for '{subscription.Plan?.Name}' (Owner: {ownerName}). Stations taken offline and confirmation email dispatched.";
        return RedirectToAction(nameof(CancellationRequests));
    }

    [HttpPost("reject-cancellation/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCancellation(Guid id, string adminRemarks)
    {
        var subscription = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Owner)
                .ThenInclude(o => o!.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subscription == null) return NotFound();

        if (subscription.Status != "CancellationPending")
        {
            TempData["Error"] = "This request is not in a pending state.";
            return RedirectToAction(nameof(CancellationRequests));
        }

        if (string.IsNullOrWhiteSpace(adminRemarks))
        {
            TempData["Error"] = "Please provide admin remarks explaining why the cancellation was rejected.";
            return RedirectToAction(nameof(CancellationRequests));
        }

        // Revert status to Active (stations remain active)
        subscription.Status = "Active";
        subscription.CancellationProcessedAt = DateTime.UtcNow;
        subscription.CancellationAdminRemarks = adminRemarks.Trim();

        await _context.SaveChangesAsync();

        // Send Email Notification to Owner
        var ownerEmail = subscription.Owner?.User?.Email;
        var ownerName = subscription.Owner?.FullName ?? subscription.Owner?.User?.Fullname ?? "Station Owner";

        if (!string.IsNullOrWhiteSpace(ownerEmail))
        {
            var emailSubject = $"VoltNet: Update on your Subscription Cancellation Request";
            var emailBody = $@"Dear {ownerName},

Your cancellation request for subscription '{subscription.Plan?.Name}' (Payment ID: {subscription.PaymentId}) has been REVIEWED and REJECTED by our administration team.

DETAILS:
---------------------------------------------
Plan Name     : {subscription.Plan?.Name}
Requested On  : {subscription.CancellationRequestedAt:dd MMM yyyy, hh:mm tt} UTC
Admin Remarks : {subscription.CancellationAdminRemarks}
Current Status: Active (Your stations remain active and operational)

If you believe this decision was made in error, please contact support at support@voltnet.com.

Best regards,
VoltNet Admin Team";

            await SendEmailNotificationAsync(ownerEmail, emailSubject, emailBody);
        }

        TempData["Success"] = $"Cancellation rejected. Subscription remains Active for '{ownerName}'. Email dispatched.";
        return RedirectToAction(nameof(CancellationRequests));
    }

    private async Task SendEmailNotificationAsync(string toEmail, string subject, string body)
    {
        try
        {
            var client = new HttpClient();
            var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent(toEmail), "to");
            formContent.Add(new StringContent(subject), "subject");
            formContent.Add(new StringContent(body), "body");

            await client.PostAsync("http://mailsendapi.runasp.net/api/Mailing/send", formContent);
        }
        catch
        {
            // Silently log or continue without throwing to prevent breaking the HTTP response flow
        }
    }
}
