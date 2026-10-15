using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationowner;

public class StationPerformanceDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = null!;
    public string City { get; set; } = null!;
    public int ChargersCount { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalKwh { get; set; }
    public decimal TotalRevenue { get; set; }
}

[Authorize(Roles = "StationOwner")]
[Route("owner/reports")]
public class OwnerReportsController : Controller
{
    private readonly AppDbContext _context;

    public OwnerReportsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    private async Task<StationOwner?> GetCurrentOwnerAsync()
    {
        var userId = GetUserId();
        return await _context.StationOwners.FirstOrDefaultAsync(o => o.UserId == userId);
    }

    // GET: /owner/reports
    [HttpGet("")]
    public async Task<IActionResult> Index(int? days)
    {
        var userId = GetUserId();
        var stations = await _context.Stations
            .Include(s => s.Chargers)
            .Where(s => s.OwnerUserId == userId)
            .ToListAsync();

        var stationIds = stations.Select(s => s.Id).ToList();

        var bookingsQuery = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Where(b => stationIds.Contains(b.StationId) && (b.Status == "Completed" || b.Status == "Confirmed"))
            .AsQueryable();

        if (days.HasValue && days.Value > 0)
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(-days.Value);
            bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= cutoff);
        }

        var bookings = await bookingsQuery.ToListAsync();

        decimal totalRevenue = bookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        decimal totalKwh = bookings.Sum(b => b.UnitsConsumedKwh ?? 0);
        int totalSessions = bookings.Count;

        // Station-wise metrics
        var stationSummaries = new List<StationPerformanceDto>();
        foreach (var st in stations)
        {
            var stBookings = bookings.Where(b => b.StationId == st.Id).ToList();
            stationSummaries.Add(new StationPerformanceDto
            {
                StationId = st.Id,
                StationName = st.Name,
                City = st.City,
                ChargersCount = st.Chargers.Count,
                TotalSessions = stBookings.Count,
                TotalKwh = stBookings.Sum(b => b.UnitsConsumedKwh ?? 0),
                TotalRevenue = stBookings.Sum(b => b.ActualCost ?? b.EstimatedCost)
            });
        }

        // Monthly trends (past 6 months)
        var now = DateTime.UtcNow;
        var monthLabels = new List<string>();
        var monthRevenue = new List<decimal>();
        for (int i = 5; i >= 0; i--)
        {
            var m = now.AddMonths(-i);
            monthLabels.Add(m.ToString("MMM yyyy"));
            var rev = bookings.Where(b => b.BookingDate.Year == m.Year && b.BookingDate.Month == m.Month)
                              .Sum(b => b.ActualCost ?? b.EstimatedCost);
            monthRevenue.Add(rev);
        }

        // Connector distribution
        var connectorGroups = bookings
            .Where(b => b.Charger != null && !string.IsNullOrEmpty(b.Charger.ConnectorType))
            .GroupBy(b => b.Charger!.ConnectorType)
            .Select(g => new { Connector = g.Key, Count = g.Count() })
            .ToList();

        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.TotalKwh = totalKwh;
        ViewBag.TotalSessions = totalSessions;
        ViewBag.StationsCount = stations.Count;
        ViewBag.SelectedDays = days;
        ViewBag.MonthLabels = monthLabels;
        ViewBag.MonthRevenue = monthRevenue;
        ViewBag.ConnectorLabels = connectorGroups.Select(c => c.Connector).ToList();
        ViewBag.ConnectorCounts = connectorGroups.Select(c => c.Count).ToList();

        return View("~/Views/stationowner/Reports.cshtml", stationSummaries);
    }
}
