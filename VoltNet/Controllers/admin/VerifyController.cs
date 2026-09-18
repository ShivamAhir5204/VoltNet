using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using System.Security.Claims;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "Admin,SuperAdmin")]
public class VerifyController : Controller
{
    private readonly AppDbContext _context;

    public VerifyController(AppDbContext context)
    {
        _context = context;
    }

    // ── Verify Stations ─────────────────────────────────────────

    [HttpGet("admin/VerifyStations")]
    public async Task<IActionResult> VerifyStations()
    {
        var pendingStations = await _context.Stations
            .Include(s => s.OwnerUser)
            .Where(s => s.Status == "PendingVerification")
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View("~/Views/admin/verify/VerifyStations.cshtml", pendingStations);
    }

    [HttpPost("admin/VerifyStations/Details")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StationDetails(Guid id)
    {
        var station = await _context.Stations
            .Include(s => s.OwnerUser)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station == null) return NotFound();

        return View("~/Views/admin/verify/StationDetails.cshtml", station);
    }

    [HttpPost("admin/VerifyStations/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveStation(Guid id)
    {
        var station = await _context.Stations.FindAsync(id);
        if (station == null) return NotFound();

        station.Status = "Active";
        station.RejectionReason = null;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station '{station.Name}' approved successfully.";
        return RedirectToAction(nameof(VerifyStations));
    }

    [HttpPost("admin/VerifyStations/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectStation(Guid id, string rejectionReason)
    {
        var station = await _context.Stations.FindAsync(id);
        if (station == null) return NotFound();

        station.Status = "Rejected";
        station.RejectionReason = rejectionReason;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station '{station.Name}' has been rejected.";
        return RedirectToAction(nameof(VerifyStations));
    }

    // ── Verify Owners (Become Partner) ──────────────────────────

    [HttpGet("admin/VerifyOwners")]
    public async Task<IActionResult> VerifyOwners()
    {
        var pendingOwners = await _context.StationOwners
            .Include(o => o.User)
            .Where(o => o.Status == "Pending")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View("~/Views/admin/verify/VerifyOwners.cshtml", pendingOwners);
    }

    [HttpPost("admin/VerifyOwners/Details")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OwnerDetails(Guid id)
    {
        var owner = await _context.StationOwners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (owner == null) return NotFound();

        return View("~/Views/admin/verify/OwnerDetails.cshtml", owner);
    }

    [HttpPost("admin/VerifyOwners/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveOwner(Guid id)
    {
        var owner = await _context.StationOwners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (owner == null) return NotFound();

        var adminId = User.FindFirstValue("AdminId");

        owner.Status = "Approved";
        owner.ApprovedAt = DateTime.UtcNow;
        // Not setting ApprovedBy as it expects a UserMaster ID but we have an AdminUser ID
        owner.RejectionReason = null;

        // Change user role to StationOwner
        owner.User.Role = "StationOwner";

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Partner application for '{owner.FullName}' approved. User role updated to Station Owner.";
        return RedirectToAction(nameof(VerifyOwners));
    }

    [HttpPost("admin/VerifyOwners/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectOwner(Guid id, string rejectionReason)
    {
        var owner = await _context.StationOwners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (owner == null) return NotFound();

        owner.Status = "Rejected";
        owner.RejectionReason = rejectionReason;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Partner application for '{owner.FullName}' has been rejected.";
        return RedirectToAction(nameof(VerifyOwners));
    }
}
