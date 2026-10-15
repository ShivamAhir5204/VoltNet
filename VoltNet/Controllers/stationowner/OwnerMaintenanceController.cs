using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/maintenance")]
public class OwnerMaintenanceController : Controller
{
    private readonly AppDbContext _context;

    public OwnerMaintenanceController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    private async Task<StationOwner?> GetCurrentOwnerAsync()
    {
        var userId = GetUserId();
        return await _context.StationOwners.FirstOrDefaultAsync(o => o.UserId == userId);
    }

    // GET: /owner/maintenance
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? stationId, string? status, string? priority)
    {
        var userId = GetUserId();
        var ownerStations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var stationIds = ownerStations.Select(s => s.Id).ToList();

        var query = _context.MaintenanceTickets
            .Include(t => t.Station)
            .Include(t => t.Charger)
            .Include(t => t.ReportedBy)
            .Where(t => stationIds.Contains(t.StationId))
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

        var allTickets = await _context.MaintenanceTickets
            .Where(t => stationIds.Contains(t.StationId))
            .ToListAsync();

        ViewBag.TotalTickets = allTickets.Count;
        ViewBag.OpenTickets = allTickets.Count(t => t.Status == "Open");
        ViewBag.InProgressTickets = allTickets.Count(t => t.Status == "InProgress");
        ViewBag.ResolvedTickets = allTickets.Count(t => t.Status == "Resolved" || t.Status == "Closed");

        ViewBag.Stations = ownerStations;
        ViewBag.SelectedStationId = stationId;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedPriority = priority;

        return View("~/Views/stationowner/Maintenance/Index.cshtml", tickets);
    }
}
