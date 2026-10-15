using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.admin;

public class AdminStationRankDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    public int ChargersCount { get; set; }
    public int SessionsCount { get; set; }
    public decimal TotalKwh { get; set; }
    public decimal TotalRevenue { get; set; }
}

[Authorize(Roles = "SuperAdmin, Admin")]
[Route("admin/reports")]
public class AdminReportsController : Controller
{
    private readonly AppDbContext _context;

    public AdminReportsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: /admin/reports
    [HttpGet("")]
    public async Task<IActionResult> Index(int? days)
    {
        var bookingsQuery = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Where(b => b.Status == "Completed" || b.Status == "Confirmed")
            .AsQueryable();

        if (days.HasValue && days.Value > 0)
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(-days.Value);
            bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= cutoff);
        }

        var bookings = await bookingsQuery.ToListAsync();

        decimal platformRevenue = bookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        decimal platformKwh = bookings.Sum(b => b.UnitsConsumedKwh ?? 0);
        int totalSessions = bookings.Count;

        // Platform Counts
        var totalUsers = await _context.UserMasters.CountAsync();
        var totalCustomers = await _context.UserMasters.CountAsync(u => u.Role == "Customer");
        var totalOwners = await _context.StationOwners.CountAsync();
        var totalManagers = await _context.StationManagers.CountAsync();
        var totalStations = await _context.Stations.CountAsync();
        var verifiedStations = await _context.Stations.CountAsync(s => s.Status == "Active");
        var totalChargers = await _context.Chargers.CountAsync();
        var activeChargers = await _context.Chargers.CountAsync(c => c.Status == "Active");

        var totalTickets = await _context.MaintenanceTickets.CountAsync();
        var openTickets = await _context.MaintenanceTickets.CountAsync(t => t.Status == "Open" || t.Status == "InProgress");

        // Station-wise rankings
        var allStations = await _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.OwnerUser)
            .ToListAsync();

        var stationRankings = new List<AdminStationRankDto>();
        foreach (var st in allStations)
        {
            var stBookings = bookings.Where(b => b.StationId == st.Id).ToList();
            stationRankings.Add(new AdminStationRankDto
            {
                StationId = st.Id,
                StationName = st.Name,
                City = st.City,
                State = st.State,
                OwnerName = st.OwnerUser?.Fullname ?? "Partner",
                ChargersCount = st.Chargers.Count,
                SessionsCount = stBookings.Count,
                TotalKwh = stBookings.Sum(b => b.UnitsConsumedKwh ?? 0),
                TotalRevenue = stBookings.Sum(b => b.ActualCost ?? b.EstimatedCost)
            });
        }

        stationRankings = stationRankings.OrderByDescending(s => s.TotalRevenue).ToList();

        // Past 6 Months Platform Trends
        var now = DateTime.UtcNow;
        var monthLabels = new List<string>();
        var monthRevenue = new List<decimal>();
        var monthSessions = new List<int>();

        for (int i = 5; i >= 0; i--)
        {
            var m = now.AddMonths(-i);
            monthLabels.Add(m.ToString("MMM yyyy"));
            var mBookings = bookings.Where(b => b.BookingDate.Year == m.Year && b.BookingDate.Month == m.Month).ToList();
            monthRevenue.Add(mBookings.Sum(b => b.ActualCost ?? b.EstimatedCost));
            monthSessions.Add(mBookings.Count);
        }

        // Connector Distribution across platform
        var connectorGroups = bookings
            .Where(b => b.Charger != null && !string.IsNullOrEmpty(b.Charger.ConnectorType))
            .GroupBy(b => b.Charger!.ConnectorType)
            .Select(g => new { Connector = g.Key, Count = g.Count() })
            .ToList();

        ViewBag.PlatformRevenue = platformRevenue;
        ViewBag.PlatformKwh = platformKwh;
        ViewBag.TotalSessions = totalSessions;
        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalCustomers = totalCustomers;
        ViewBag.TotalOwners = totalOwners;
        ViewBag.TotalManagers = totalManagers;
        ViewBag.TotalStations = totalStations;
        ViewBag.VerifiedStations = verifiedStations;
        ViewBag.TotalChargers = totalChargers;
        ViewBag.ActiveChargers = activeChargers;
        ViewBag.TotalTickets = totalTickets;
        ViewBag.OpenTickets = openTickets;
        ViewBag.SelectedDays = days;

        ViewBag.MonthLabels = monthLabels;
        ViewBag.MonthRevenue = monthRevenue;
        ViewBag.MonthSessions = monthSessions;
        ViewBag.ConnectorLabels = connectorGroups.Select(c => c.Connector).ToList();
        ViewBag.ConnectorCounts = connectorGroups.Select(c => c.Count).ToList();

        return View("~/Views/admin/Reports.cshtml", stationRankings);
    }
}
