using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/subscription")]
public class OwnerSubscriptionController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public OwnerSubscriptionController(AppDbContext context, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);
    private Guid GetOwnerId() => Guid.Parse(User.FindFirstValue("StationOwnerId")!);

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var ownerId = GetOwnerId();
        var allSubscriptions = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Stations)
            .Where(s => s.StationOwnerId == ownerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        // ── Real-World Accurate Stats Calculation ──
        // 1. Active Plans: Plans that are currently Active (and not expired or undergoing cancellation)
        var activeCount = allSubscriptions.Count(s => s.Status == "Active" && s.EndDate >= DateTime.UtcNow);
        
        // 2. Pending Cancellation Plans:
        var pendingCancellationCount = allSubscriptions.Count(s => s.Status == "CancellationPending");

        // 3. Station Usage for actively valid plans
        var totalStationsUsed = allSubscriptions
            .Where(s => (s.Status == "Active" || s.Status == "CancellationPending") && s.EndDate >= DateTime.UtcNow)
            .Sum(s => s.Stations?.Count ?? 0);

        var totalStationsAllowed = allSubscriptions
            .Where(s => (s.Status == "Active" || s.Status == "CancellationPending") && s.EndDate >= DateTime.UtcNow)
            .Sum(s => s.Plan?.MaxStations ?? 0);

        // 4. Total Invested: ONLY non-cancelled plans (if cancelled/refunded, amount is deducted from net investment)
        var totalInvested = allSubscriptions
            .Where(s => s.Status != "Cancelled")
            .Sum(s => s.AmountPaid);

        // 5. Total Subscriptions count (Active or History)
        // Valid active subscriptions count (Active + CancellationPending)
        var activeSubscriptionsCount = allSubscriptions.Count(s => (s.Status == "Active" || s.Status == "CancellationPending") && s.EndDate >= DateTime.UtcNow);
        var historyCount = allSubscriptions.Count(s => s.Status == "Cancelled" || (s.Status != "CancellationPending" && s.EndDate < DateTime.UtcNow));

        ViewBag.ActiveCount = activeCount;
        ViewBag.PendingCancellationCount = pendingCancellationCount;
        ViewBag.TotalStationsUsed = totalStationsUsed;
        ViewBag.TotalStationsAllowed = totalStationsAllowed;
        ViewBag.TotalSpent = totalInvested;
        ViewBag.TotalCount = allSubscriptions.Count(s => s.Status != "Cancelled");
        ViewBag.HistoryCount = historyCount;

        // Display Active + CancellationPending plans on the main dashboard
        var activeSubscriptions = allSubscriptions
            .Where(s => (s.Status == "Active" || s.Status == "CancellationPending") && s.EndDate >= DateTime.UtcNow)
            .ToList();

        return View("~/Views/stationowner/Subscription/Index.cshtml", activeSubscriptions);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var ownerId = GetOwnerId();
        var historySubscriptions = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Stations)
            .Where(s => s.StationOwnerId == ownerId && (s.Status == "Cancelled" || (s.Status != "CancellationPending" && s.EndDate < DateTime.UtcNow)))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var allSubscriptions = await _context.OwnerSubscriptions
            .Where(s => s.StationOwnerId == ownerId)
            .ToListAsync();

        ViewBag.ActiveCount = allSubscriptions.Count(s => (s.Status == "Active" || s.Status == "CancellationPending") && s.EndDate >= DateTime.UtcNow);
        ViewBag.HistoryCount = historySubscriptions.Count;

        return View("~/Views/stationowner/Subscription/History.cshtml", historySubscriptions);
    }

    [HttpGet("plan/{id}")]
    public async Task<IActionResult> PlanDetails(Guid id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null)
        {
            TempData["Error"] = "Plan not found.";
            return RedirectToAction(nameof(Plans));
        }

        ViewBag.RazorpayKeyId = _config["Razorpay:KeyId"];
        return View("~/Views/stationowner/Subscription/PlanDetails.cshtml", plan);
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans()
    {
        var plans = await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.PricePerMonth)
            .ToListAsync();

        // Auto-seed plans if none exist for easy testing
        if (!plans.Any())
        {
            var seedPlans = new List<SubscriptionPlan>
            {
                new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Starter", Description = "Basic plan for 1 station", PricePerMonth = 999, MaxStations = 1, MaxManagersPerStation = 2, DurationDays = 30 },
                new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Growth", Description = "Perfect for expanding networks up to 5 stations", PricePerMonth = 3999, MaxStations = 5, MaxManagersPerStation = 5, DurationDays = 30 },
                new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Enterprise", Description = "Unlimited scale for up to 20 stations", PricePerMonth = 9999, MaxStations = 20, MaxManagersPerStation = 10, DurationDays = 30 }
            };
            _context.SubscriptionPlans.AddRange(seedPlans);
            await _context.SaveChangesAsync();
            plans = seedPlans.OrderBy(p => p.PricePerMonth).ToList();
        }

        ViewBag.RazorpayKeyId = _config["Razorpay:KeyId"];
        return View("~/Views/stationowner/Subscription/Plans.cshtml", plans);
    }

    // ─── Step 1: Simulate Order Creation ─────────────────────────────
    [HttpPost("create-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder([FromForm] Guid planId)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null || !plan.IsActive)
        {
            return Json(new { success = false, message = "Invalid or inactive plan." });
        }

        var amountInPaise = (long)(plan.PricePerMonth * 100);
        var orderId = $"ORDER_SIM_{Guid.NewGuid().ToString()[..8].ToUpper()}";

        // Fetch owner info for prefill
        var userId = GetUserId();
        var user = await _context.UserMasters.FindAsync(userId);

        // Simulate a slight network delay
        await Task.Delay(800);

        return Json(new
        {
            success = true,
            orderId,
            amount = amountInPaise,
            currency = "INR",
            planId = plan.Id,
            planName = plan.Name,
            prefill = new
            {
                name = user?.Fullname ?? "",
                email = user?.Email ?? "",
                contact = user?.Mobile ?? ""
            }
        });
    }

    // ─── Step 2: Simulate Payment Verification ───────────────────────
    [HttpPost("verify-payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPayment(
        [FromForm] string razorpay_order_id,
        [FromForm] string razorpay_payment_id,
        [FromForm] Guid planId)
    {
        // ── Validation 1: Check parameters ──
        if (string.IsNullOrWhiteSpace(razorpay_order_id) || string.IsNullOrWhiteSpace(razorpay_payment_id))
        {
            TempData["Error"] = "Payment verification failed. Missing payment details.";
            return RedirectToAction(nameof(Plans));
        }

        // ── Validation 2: Plan must still be valid ──
        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null || !plan.IsActive)
        {
            TempData["Error"] = "The selected plan is no longer available.";
            return RedirectToAction(nameof(Plans));
        }

        // ── Validation 3: Prevent duplicate subscription for same payment ──
        var ownerId = GetOwnerId();
        var alreadyExists = await _context.OwnerSubscriptions
            .AnyAsync(s => s.PaymentId == razorpay_payment_id);

        if (alreadyExists)
        {
            TempData["Error"] = "This payment has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        // ── All checks passed — Create Subscription ──
        var subscription = new OwnerSubscription
        {
            Id = Guid.NewGuid(),
            StationOwnerId = ownerId,
            PlanId = plan.Id,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(plan.DurationDays),
            Status = "Active",
            AmountPaid = plan.PricePerMonth,
            PaymentId = razorpay_payment_id,
            CreatedAt = DateTime.UtcNow
        };

        _context.OwnerSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Payment successful! You are now subscribed to the {plan.Name} plan. Assign your stations to activate them.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id}/assign")]
    public async Task<IActionResult> Assign(Guid id)
    {
        var ownerId = GetOwnerId();
        var subscription = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Stations)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationOwnerId == ownerId);

        if (subscription == null)
            return NotFound();

        var userId = GetUserId();
        var allStations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .ToListAsync();

        ViewBag.AllStations = allStations;
        return View("~/Views/stationowner/Subscription/Assign.cshtml", subscription);
    }

    [HttpPost("{id}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPost(Guid id, List<Guid> stationIds)
    {
        var ownerId = GetOwnerId();
        var subscription = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Stations)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationOwnerId == ownerId);

        if (subscription == null) return NotFound();

        if (stationIds == null) stationIds = new List<Guid>();

        if (stationIds.Count > subscription.Plan!.MaxStations)
        {
            TempData["Error"] = $"You can only assign up to {subscription.Plan.MaxStations} stations for this plan.";
            return RedirectToAction("Assign", new { id });
        }

        var userId = GetUserId();
        var myStations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .ToListAsync();

        // Unassign stations not in the list
        foreach (var station in myStations.Where(s => s.OwnerSubscriptionId == subscription.Id))
        {
            if (!stationIds.Contains(station.Id))
            {
                station.OwnerSubscriptionId = null;
                // If it loses its subscription, hide it from public (fallback to Approved or PendingVerification)
                if (station.Status == "Active") 
                    station.Status = "Approved"; 
            }
        }

        // Assign checked stations
        foreach (var stationId in stationIds)
        {
            var station = myStations.FirstOrDefault(s => s.Id == stationId);
            if (station != null)
            {
                // Assign to this subscription
                station.OwnerSubscriptionId = subscription.Id;
                
                // Real world: If it was Approved by Admin, it now becomes fully Active
                if (station.Status == "Approved")
                {
                    station.Status = "Active";
                }
            }
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Stations successfully assigned to the subscription.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Subscription Details ─────────────────────────────────────────
    [HttpGet("{id}/details")]
    public async Task<IActionResult> Details(Guid id)
    {
        var ownerId = GetOwnerId();
        var subscription = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Stations)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationOwnerId == ownerId);

        if (subscription == null) return NotFound();

        return View("~/Views/stationowner/Subscription/Details.cshtml", subscription);
    }

    // ─── Request Subscription Cancellation ──────────────────────────
    [HttpPost("{id}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string cancellationReason)
    {
        var ownerId = GetOwnerId();
        var subscription = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Stations)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationOwnerId == ownerId);

        if (subscription == null) return NotFound();

        // ── Validation 1: Prevent duplicate requests or invalid statuses ──
        if (subscription.Status == "CancellationPending")
        {
            TempData["Error"] = "A cancellation request for this subscription is already pending Admin approval.";
            return RedirectToAction(nameof(Index));
        }

        if (subscription.Status != "Active")
        {
            TempData["Error"] = "This subscription is no longer active and cannot be cancelled.";
            return RedirectToAction(nameof(Index));
        }

        if (subscription.EndDate < DateTime.UtcNow)
        {
            TempData["Error"] = "This subscription has already expired. Cancellation is not applicable.";
            return RedirectToAction(nameof(Index));
        }

        // ── Validation 2: Enforce 24-Hour Purchase Window Policy ──
        var elapsed = DateTime.UtcNow - subscription.CreatedAt;
        if (elapsed.TotalHours > 24)
        {
            TempData["Error"] = $"Cancellation is only permitted within 24 hours of purchase. Purchased on {subscription.CreatedAt:dd MMM yyyy, hh:mm tt} UTC ({Math.Floor(elapsed.TotalHours)} hours ago). Policy window has expired.";
            return RedirectToAction(nameof(Index));
        }

        // ── Validation 3: Mandatory Reason with Minimum Length Check ──
        if (string.IsNullOrWhiteSpace(cancellationReason) || cancellationReason.Trim().Length < 10)
        {
            TempData["Error"] = "Please provide a valid and detailed reason for cancellation (minimum 10 characters).";
            return RedirectToAction(nameof(Index));
        }

        // ── Loophole Guard: Check reason length cap ──
        var trimmedReason = cancellationReason.Trim();
        if (trimmedReason.Length > 500)
        {
            trimmedReason = trimmedReason[..500];
        }

        // ── Place Subscription into 'CancellationPending' State ──
        subscription.Status = "CancellationPending";
        subscription.CancellationReason = trimmedReason;
        subscription.CancellationRequestedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Cancellation request submitted for '{subscription.Plan?.Name}' plan. It will be reviewed by our administration team within 24 hours.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Download Receipt ─────────────────────────────────────────────
    [HttpGet("{id}/receipt")]
    public async Task<IActionResult> DownloadReceipt(Guid id)
    {
        var ownerId = GetOwnerId();
        var subscription = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationOwnerId == ownerId);

        if (subscription == null) return NotFound();

        var userId = GetUserId();
        var user = await _context.UserMasters.FindAsync(userId);

        // Generate plain text receipt
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("              VOLTNET - PAYMENT RECEIPT              ");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"  Receipt Date  : {DateTime.UtcNow:dd MMM yyyy, hh:mm tt} UTC");
        sb.AppendLine($"  Payment ID    : {subscription.PaymentId}");
        sb.AppendLine($"  Subscription  : {subscription.Id}");
        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────");
        sb.AppendLine("  CUSTOMER DETAILS");
        sb.AppendLine("───────────────────────────────────────────");
        sb.AppendLine($"  Name          : {user?.Fullname ?? "N/A"}");
        sb.AppendLine($"  Email         : {user?.Email ?? "N/A"}");
        sb.AppendLine($"  Phone         : {user?.Mobile ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────");
        sb.AppendLine("  PLAN DETAILS");
        sb.AppendLine("───────────────────────────────────────────");
        sb.AppendLine($"  Plan Name     : {subscription.Plan?.Name ?? "N/A"}");
        sb.AppendLine($"  Description   : {subscription.Plan?.Description ?? "N/A"}");
        sb.AppendLine($"  Max Stations  : {subscription.Plan?.MaxStations ?? 0}");
        sb.AppendLine($"  Duration      : {subscription.Plan?.DurationDays ?? 0} Days");
        sb.AppendLine($"  Start Date    : {subscription.StartDate:dd MMM yyyy}");
        sb.AppendLine($"  End Date      : {subscription.EndDate:dd MMM yyyy}");
        sb.AppendLine($"  Status        : {subscription.Status}");
        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────");
        sb.AppendLine("  PAYMENT SUMMARY");
        sb.AppendLine("───────────────────────────────────────────");
        sb.AppendLine($"  Amount Paid   : ₹{subscription.AmountPaid:N2}");
        sb.AppendLine($"  Payment ID    : {subscription.PaymentId}");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("  This is an auto-generated receipt from VoltNet.");
        sb.AppendLine("  For support, contact support@voltnet.com");
        sb.AppendLine("═══════════════════════════════════════════");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/plain", $"VoltNet_Receipt_{subscription.PaymentId}.txt");
    }
}
