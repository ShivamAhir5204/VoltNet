using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/bookings")]
public class OwnerBookingsController : Controller
{
    private readonly AppDbContext _context;

    public OwnerBookingsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    // GET: /owner/bookings
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? stationId, string? status, DateTime? date)
    {
        var userId = GetUserId();

        var ownerStations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var ownerStationIds = ownerStations.Select(s => s.Id).ToList();

        var query = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Customer)
            .Include(b => b.Charger)
            .Where(b => ownerStationIds.Contains(b.StationId))
            .AsQueryable();

        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            query = query.Where(b => b.StationId == stationId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(b => b.Status == status);
        }

        if (date.HasValue)
        {
            query = query.Where(b => b.BookingDate == date.Value.Date);
        }

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        ViewBag.OwnerStations = ownerStations;
        ViewBag.SelectedStationId = stationId;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd");

        return View("~/Views/stationowner/Bookings.cshtml", bookings);
    }
}
