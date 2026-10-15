using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/maintenance")]
public class ManagerMaintenanceController : Controller
{
    private readonly AppDbContext _context;

    public ManagerMaintenanceController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    private async Task<StationManager?> GetCurrentManagerAsync()
    {
        var userId = GetUserId();
        return await _context.StationManagers
            .Include(m => m.Station)
            .ThenInclude(s => s!.Chargers)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive);
    }

    // GET: /manager/maintenance
    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? priority)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
            return RedirectToAction("Error", "Home");

        var query = _context.MaintenanceTickets
            .Include(t => t.Charger)
            .Include(t => t.ReportedBy)
            .Where(t => t.StationId == manager.StationId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrEmpty(priority))
            query = query.Where(t => t.Priority == priority);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var allTickets = await _context.MaintenanceTickets
            .Where(t => t.StationId == manager.StationId)
            .ToListAsync();

        ViewBag.TotalTickets = allTickets.Count;
        ViewBag.OpenTickets = allTickets.Count(t => t.Status == "Open");
        ViewBag.InProgressTickets = allTickets.Count(t => t.Status == "InProgress");
        ViewBag.ResolvedTickets = allTickets.Count(t => t.Status == "Resolved" || t.Status == "Closed");

        ViewBag.Station = manager.Station;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedPriority = priority;

        return View("~/Views/stationmanager/Maintenance/Index.cshtml", tickets);
    }

    // GET: /manager/maintenance/create
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
            return RedirectToAction("Error", "Home");

        ViewBag.Station = manager.Station;
        ViewBag.Chargers = manager.Station.Chargers.ToList();

        return View("~/Views/stationmanager/Maintenance/Create.cshtml", new MaintenanceTicket { StationId = manager.StationId });
    }

    // POST: /manager/maintenance/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MaintenanceTicket ticket, bool setChargerMaintenance)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(ticket.Title))
            ModelState.AddModelError("Title", "Issue Title is required.");

        if (string.IsNullOrWhiteSpace(ticket.Description))
            ModelState.AddModelError("Description", "Issue Description is required.");

        if (!ModelState.IsValid)
        {
            ViewBag.Station = manager.Station;
            ViewBag.Chargers = manager.Station.Chargers.ToList();
            return View("~/Views/stationmanager/Maintenance/Create.cshtml", ticket);
        }

        ticket.Id = Guid.NewGuid();
        ticket.StationId = manager.StationId;
        ticket.ReportedById = GetUserId();
        ticket.Status = "Open";
        ticket.CreatedAt = DateTime.UtcNow;

        if (ticket.ChargerId.HasValue && ticket.ChargerId.Value != Guid.Empty)
        {
            if (setChargerMaintenance)
            {
                var charger = await _context.Chargers.FirstOrDefaultAsync(c => c.Id == ticket.ChargerId.Value && c.StationId == manager.StationId);
                if (charger != null)
                {
                    charger.Status = "Maintenance";
                }
            }
        }
        else
        {
            ticket.ChargerId = null;
        }

        _context.MaintenanceTickets.Add(ticket);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Maintenance Ticket #{ticket.Id.ToString().Substring(0, 8).ToUpper()} logged successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /manager/maintenance/update-status
    [HttpPost("update-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, string status, string? resolutionNotes, bool restoreChargerActive)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null) return Unauthorized();

        var ticket = await _context.MaintenanceTickets
            .Include(t => t.Charger)
            .FirstOrDefaultAsync(t => t.Id == id && t.StationId == manager.StationId);

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
