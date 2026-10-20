using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/revenue")]
public class ManagerRevenueController : Controller
{
    private readonly AppDbContext _context;

    public ManagerRevenueController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    private async Task<StationManager?> GetCurrentManagerAsync()
    {
        var userId = GetUserId();
        return await _context.StationManagers
            .Include(m => m.Station)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
        {
            ViewBag.ErrorMessage = "No active station assigned to your manager account.";
            return View("~/Views/stationmanager/Revenue/Index.cshtml");
        }

        var stationId = manager.StationId;
        var nowIst = DateTime.UtcNow.AddMinutes(330);
        var today = nowIst.Date;

        // Current week start (Monday)
        var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-1 * diff);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var startOf30Days = today.AddDays(-29);

        // Fetch completed bookings for this station (Strict IDOR scope)
        var completedBookings = await _context.Bookings
            .Include(b => b.Charger)
            .Where(b => b.StationId == stationId && b.Status == "Completed")
            .OrderByDescending(b => b.CompletedAt ?? b.CreatedAt)
            .ToListAsync();

        // 1. KPI Cards
        var todayBookings = completedBookings.Where(b => b.BookingDate.Date == today).ToList();
        var todayRevenue = todayBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);

        var weekBookings = completedBookings.Where(b => b.BookingDate.Date >= startOfWeek && b.BookingDate.Date <= today).ToList();
        var weekRevenue = weekBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);

        var monthBookings = completedBookings.Where(b => b.BookingDate.Date >= startOfMonth && b.BookingDate.Date <= today).ToList();
        var monthRevenue = monthBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);

        var allTimeRevenue = completedBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var totalEnergyDispensed = completedBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);

        // 2. Top Performing Charger
        string topChargerName = "—";
        string topChargerConnector = "—";
        decimal topChargerRev = 0;
        int topChargerSharePct = 0;

        var chargerRevenueGroups = completedBookings
            .Where(b => b.Charger != null)
            .GroupBy(b => b.Charger!)
            .Select(g => new
            {
                Charger = g.Key,
                Revenue = g.Sum(b => b.ActualCost ?? b.EstimatedCost),
                Count = g.Count()
            })
            .OrderByDescending(g => g.Revenue)
            .FirstOrDefault();

        if (chargerRevenueGroups != null && allTimeRevenue > 0)
        {
            topChargerName = chargerRevenueGroups.Charger.Name;
            topChargerConnector = chargerRevenueGroups.Charger.ConnectorType;
            topChargerRev = chargerRevenueGroups.Revenue;
            topChargerSharePct = (int)Math.Round((topChargerRev / allTimeRevenue) * 100);
        }

        // 3. Peak Hour Analysis
        string peakHourRange = "—";
        var peakHourGroup = completedBookings
            .GroupBy(b => b.StartTime.Hours)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (peakHourGroup != null)
        {
            var h = peakHourGroup.Key;
            var startAmPm = h >= 12 ? (h == 12 ? 12 : h - 12) + " PM" : (h == 0 ? 12 : h) + " AM";
            var nextH = (h + 1) % 24;
            var endAmPm = nextH >= 12 ? (nextH == 12 ? 12 : nextH - 12) + " PM" : (nextH == 0 ? 12 : nextH) + " AM";
            peakHourRange = $"{startAmPm} - {endAmPm}";
        }

        // 4. Bay Utilization Rate (Last 30 Days)
        var totalChargersCount = await _context.Chargers.CountAsync(c => c.StationId == stationId);
        int operatingHours = 14; // Default standard operating day (e.g. 7 AM - 9 PM)
        if (manager.Station.ClosingTime > manager.Station.OpeningTime)
        {
            operatingHours = (int)Math.Max(1, (manager.Station.ClosingTime - manager.Station.OpeningTime).TotalHours);
        }

        var totalAvailableSlots30Days = Math.Max(1, totalChargersCount * operatingHours * 30);
        var last30DaysBookings = completedBookings.Where(b => b.BookingDate.Date >= startOf30Days).ToList();
        var utilizationPct = Math.Min(100, (int)Math.Round(((double)last30DaysBookings.Count / totalAvailableSlots30Days) * 100));

        // 5. Last 30 Days Trend Data for Chart.js
        var chartLabels = new List<string>();
        var chartRevenueData = new List<decimal>();
        var chartSessionsData = new List<int>();

        for (int i = 0; i < 30; i++)
        {
            var curDate = startOf30Days.AddDays(i);
            chartLabels.Add(curDate.ToString("dd MMM"));

            var dayBookings = completedBookings.Where(b => b.BookingDate.Date == curDate).ToList();
            chartRevenueData.Add(dayBookings.Sum(b => b.ActualCost ?? b.EstimatedCost));
            chartSessionsData.Add(dayBookings.Count);
        }

        // 6. Payment Mode Breakdown
        var paymentMethodBreakdown = completedBookings
            .GroupBy(b => string.IsNullOrWhiteSpace(b.PaymentMethod) ? "UPI / QR Code" : b.PaymentMethod)
            .Select(g => new
            {
                Method = g.Key,
                Amount = g.Sum(b => b.ActualCost ?? b.EstimatedCost),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        // 7. Recent 10 Transactions
        var recentTransactions = completedBookings.Take(10).ToList();

        // View model data
        ViewBag.Station = manager.Station;
        ViewBag.TodayRevenue = todayRevenue;
        ViewBag.TodayCount = todayBookings.Count;
        ViewBag.WeekRevenue = weekRevenue;
        ViewBag.WeekCount = weekBookings.Count;
        ViewBag.MonthRevenue = monthRevenue;
        ViewBag.MonthCount = monthBookings.Count;
        ViewBag.AllTimeRevenue = allTimeRevenue;
        ViewBag.AllTimeBookings = completedBookings.Count;
        ViewBag.TotalEnergyDispensed = totalEnergyDispensed;

        ViewBag.TopChargerName = topChargerName;
        ViewBag.TopChargerConnector = topChargerConnector;
        ViewBag.TopChargerSharePct = topChargerSharePct;
        ViewBag.TopChargerRev = topChargerRev;

        ViewBag.PeakHourRange = peakHourRange;
        ViewBag.UtilizationPct = utilizationPct;

        ViewBag.ChartLabels = chartLabels;
        ViewBag.ChartRevenue = chartRevenueData;
        ViewBag.ChartSessions = chartSessionsData;

        ViewBag.PaymentBreakdown = paymentMethodBreakdown;
        ViewBag.RecentTransactions = recentTransactions;

        return View("~/Views/stationmanager/Revenue/Index.cshtml");
    }
}
