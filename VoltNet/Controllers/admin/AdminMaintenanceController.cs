using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin, Admin")]
[Route("admin/maintenance")]
public class AdminMaintenanceController : Controller
{
    private readonly AppDbContext _context;

    public AdminMaintenanceController(AppDbContext context)
    {
        _context = context;
    }

    // GET: /admin/maintenance
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? stationId, string? status, string? priority)
    {
        var query = _context.MaintenanceTickets
            .Include(t => t.Station)
            .Include(t => t.Charger)
            .Include(t => t.ReportedBy)
            .AsQueryable();

        if (stationId.HasValue && stationId.Value != Guid.Empty)
            query = query.Where(t => t.StationId == stationId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrEmpty(priority))
            query = query.Where(t => t.Priority == priority);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var allTickets = await _context.MaintenanceTickets.ToListAsync();

        ViewBag.TotalTickets = allTickets.Count;
        ViewBag.OpenTickets = allTickets.Count(t => t.Status == "Open");
        ViewBag.InProgressTickets = allTickets.Count(t => t.Status == "InProgress");
        ViewBag.ResolvedTickets = allTickets.Count(t => t.Status == "Resolved" || t.Status == "Closed");

        ViewBag.Stations = await _context.Stations.OrderBy(s => s.Name).ToListAsync();
        ViewBag.SelectedStationId = stationId;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedPriority = priority;

        return View("~/Views/admin/Maintenance/Index.cshtml", tickets);
    }

    // POST: /admin/maintenance/update-status
    [HttpPost("update-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, string status, string? resolutionNotes, bool restoreChargerActive)
    {
        var ticket = await _context.MaintenanceTickets
            .Include(t => t.Charger)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();

        ticket.Status = status;
        if (!string.IsNullOrWhiteSpace(resolutionNotes))
        {
            ticket.ResolutionNotes = resolutionNotes.Trim();
        }

        if (status == "Resolved" || status == "Closed")
        {
            ticket.ResolvedAt = DateTime.UtcNow;

            if (restoreChargerActive && ticket.Charger != null)
            {
                ticket.Charger.Status = "Active";
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Ticket #{ticket.Id.ToString().Substring(0, 8).ToUpper()} status updated to {status}.";
        return RedirectToAction(nameof(Index));
    }
}
